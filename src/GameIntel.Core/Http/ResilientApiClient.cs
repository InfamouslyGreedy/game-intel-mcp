using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GameIntel.Core.Http;

/// <summary>
/// A "polite" JSON client for third-party game APIs. Every request goes through, in order:
/// <list type="number">
///   <item>an in-memory cache with a per-call freshness window,</item>
///   <item>a shared token-bucket rate limiter,</item>
///   <item>conditional revalidation with ETags (a 304 costs the upstream almost nothing),</item>
///   <item>retries with exponential backoff + jitter that honour <c>Retry-After</c>,</item>
///   <item>stale-if-error: if the upstream is down, serve the last good copy instead of failing.</item>
/// </list>
/// Free community APIs (OpenDota, poe.ninja) are the reason this exists: they ask clients to cache
/// and throttle, and they block clients that don't.
/// </summary>
public sealed partial class ResilientApiClient
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly RateLimiter _limiter;
    private readonly ApiClientOptions _options;
    private readonly JsonSerializerOptions _json;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly string _cachePrefix;

    public ResilientApiClient(
        string name,
        HttpClient http,
        IMemoryCache cache,
        RateLimiter limiter,
        ApiClientOptions options,
        JsonSerializerOptions json,
        TimeProvider time,
        ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _limiter = limiter ?? throw new ArgumentNullException(nameof(limiter));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _json = json ?? throw new ArgumentNullException(nameof(json));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cachePrefix = $"api:{name}:";
    }

    /// <summary>
    /// GETs <paramref name="relativeUrl"/> and deserializes it, reusing a cached copy while it is
    /// younger than <paramref name="freshFor"/>.
    /// </summary>
    /// <exception cref="NotFoundException">The upstream returned 404.</exception>
    /// <exception cref="UpstreamUnavailableException">Retries were exhausted and nothing was cached.</exception>
    public async Task<T> GetJsonAsync<T>(string relativeUrl, TimeSpan freshFor, CancellationToken cancellationToken = default)
    {
        var body = await GetBodyAsync(relativeUrl, freshFor, cancellationToken).ConfigureAwait(false);

        try
        {
            return JsonSerializer.Deserialize<T>(body, _json)
                ?? throw new UpstreamUnavailableException($"Upstream returned an empty payload for '{relativeUrl}'.");
        }
        catch (JsonException ex)
        {
            // Community APIs have no SLA; a schema change should surface as a clear upstream error.
            throw new UpstreamUnavailableException($"Upstream response for '{relativeUrl}' had an unexpected shape.", ex);
        }
    }

    internal async Task<string> GetBodyAsync(string relativeUrl, TimeSpan freshFor, CancellationToken cancellationToken)
    {
        var key = _cachePrefix + relativeUrl;
        var now = _time.GetUtcNow();

        _cache.TryGetValue(key, out CachedResponse? cached);
        if (cached is not null && cached.FreshUntil > now)
        {
            LogCacheHit(relativeUrl);
            return cached.Body;
        }

        try
        {
            var result = await SendWithRetriesAsync(relativeUrl, cached?.ETag, cancellationToken).ConfigureAwait(false);

            string body;
            string? etag;
            if (result.NotModified && cached is not null)
            {
                LogRevalidated(relativeUrl);
                body = cached.Body;
                etag = cached.ETag;
            }
            else
            {
                body = result.Body!;
                etag = result.ETag;
            }

            Store(key, body, etag, freshFor);
            return body;
        }
        catch (UpstreamUnavailableException ex) when (cached is not null)
        {
            LogServingStale(ex, relativeUrl);
            return cached.Body;
        }
    }

    private void Store(string key, string body, string? etag, TimeSpan freshFor)
    {
        var entry = new CachedResponse(body, etag, _time.GetUtcNow() + freshFor);
        _cache.Set(key, entry, new MemoryCacheEntryOptions
        {
            // Keep the entry past its freshness window so it can be revalidated or served on error.
            AbsoluteExpirationRelativeToNow = freshFor + _options.StaleRetention,
            Size = body.Length,
        });
    }

    private async Task<SendResult> SendWithRetriesAsync(string relativeUrl, string? etag, CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (var attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            if (attempt > 0)
            {
                var delay = lastError is RetryAfterException ra && ra.RetryAfter is { } hinted
                    ? hinted
                    : BackoffDelay(attempt);
                LogRetrying(relativeUrl, attempt, delay.TotalMilliseconds);
                await Task.Delay(delay, _time, cancellationToken).ConfigureAwait(false);
            }

            using var lease = await _limiter.AcquireAsync(1, cancellationToken).ConfigureAwait(false);
            if (!lease.IsAcquired)
            {
                throw new UpstreamUnavailableException("Too many queued requests to the upstream API; try again shortly.");
            }

            try
            {
                return await SendOnceAsync(relativeUrl, etag, cancellationToken).ConfigureAwait(false);
            }
            catch (RetryAfterException ex)
            {
                lastError = ex;
            }
            catch (HttpRequestException ex)
            {
                lastError = ex;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // HttpClient timeout, not caller cancellation.
                lastError = ex;
            }
        }

        throw new UpstreamUnavailableException(
            $"The upstream API did not respond successfully after {_options.MaxRetries + 1} attempts.", lastError!);
    }

    private async Task<SendResult> SendOnceAsync(string relativeUrl, string? etag, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        if (etag is not null && EntityTagHeaderValue.TryParse(etag, out var tag))
        {
            request.Headers.IfNoneMatch.Add(tag);
        }

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return new SendResult(null, etag, NotModified: true);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new NotFoundException($"The upstream API has no data at '{relativeUrl}'.");
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
        {
            throw new RetryAfterException(response.StatusCode, RetryAfter(response));
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new UpstreamUnavailableException(
                $"The upstream API rejected the request with {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new SendResult(body, response.Headers.ETag?.ToString(), NotModified: false);
    }

    private TimeSpan BackoffDelay(int attempt)
    {
        var exponential = _options.BaseRetryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
#pragma warning disable CA5394 // Jitter does not need a cryptographic RNG.
        var jitter = Random.Shared.NextDouble() * _options.BaseRetryDelay.TotalMilliseconds;
#pragma warning restore CA5394
        return TimeSpan.FromMilliseconds(exponential + jitter);
    }

    private TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta)
        {
            return delta;
        }

        if (header?.Date is { } date)
        {
            var wait = date - _time.GetUtcNow();
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        return null;
    }

    private sealed record CachedResponse(string Body, string? ETag, DateTimeOffset FreshUntil);

    private sealed record SendResult(string? Body, string? ETag, bool NotModified);

    private sealed class RetryAfterException(HttpStatusCode status, TimeSpan? retryAfter)
        : Exception($"Upstream returned {(int)status}.")
    {
        public TimeSpan? RetryAfter { get; } = retryAfter;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cache hit for {Url}")]
    private partial void LogCacheHit(string url);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Upstream confirmed {Url} unchanged (304)")]
    private partial void LogRevalidated(string url);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Retrying {Url} (attempt {Attempt}) in {DelayMs:F0} ms")]
    private partial void LogRetrying(string url, int attempt, double delayMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Upstream unavailable for {Url}; serving stale cached copy")]
    private partial void LogServingStale(Exception ex, string url);
}
