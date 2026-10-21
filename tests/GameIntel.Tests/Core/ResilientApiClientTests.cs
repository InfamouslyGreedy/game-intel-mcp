using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.RateLimiting;
using GameIntel.Core;
using GameIntel.Core.Http;
using GameIntel.Tests.TestDoubles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameIntel.Tests.Core;

public sealed class ResilientApiClientTests : IDisposable
{
    private readonly StubHttpHandler _handler = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly RateLimiter _limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions { PermitLimit = 100, QueueLimit = 0 });
    private readonly HttpClient _http;
    private readonly ResilientApiClient _client;

    private sealed record Payload(int Value);

    public ResilientApiClientTests()
    {
        _http = new HttpClient(_handler) { BaseAddress = new Uri("https://api.example.test/") };
        _client = new ResilientApiClient(
            "test",
            _http,
            _cache,
            _limiter,
            new ApiClientOptions { MaxRetries = 2, BaseRetryDelay = TimeSpan.Zero },
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            _clock,
            NullLogger.Instance);
    }

    [Fact]
    public async Task Fresh_cache_entry_is_served_without_a_request()
    {
        _handler.Respond(HttpStatusCode.OK, """{"value":1}""");

        var first = await _client.GetJsonAsync<Payload>("thing", TimeSpan.FromMinutes(5));
        _clock.Advance(TimeSpan.FromMinutes(4));
        var second = await _client.GetJsonAsync<Payload>("thing", TimeSpan.FromMinutes(5));

        Assert.Equal(1, first.Value);
        Assert.Equal(1, second.Value);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task Stale_entry_is_revalidated_with_its_etag_and_reused_on_304()
    {
        _handler
            .Respond(HttpStatusCode.OK, """{"value":7}""", r => r.Headers.ETag = new EntityTagHeaderValue("\"v1\""))
            .Respond(HttpStatusCode.NotModified);

        await _client.GetJsonAsync<Payload>("thing", TimeSpan.FromMinutes(5));
        _clock.Advance(TimeSpan.FromMinutes(6));
        var revalidated = await _client.GetJsonAsync<Payload>("thing", TimeSpan.FromMinutes(5));

        Assert.Equal(7, revalidated.Value);
        Assert.Equal(2, _handler.Requests.Count);
        Assert.Equal("\"v1\"", _handler.Requests[1].Headers.IfNoneMatch.Single().Tag);
    }

    [Fact]
    public async Task Rate_limited_and_server_errors_are_retried()
    {
        _handler
            .Respond(HttpStatusCode.TooManyRequests, "", r => r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero))
            .Respond(HttpStatusCode.BadGateway)
            .Respond(HttpStatusCode.OK, """{"value":3}""");

        var result = await _client.GetJsonAsync<Payload>("flaky", TimeSpan.FromMinutes(1));

        Assert.Equal(3, result.Value);
        Assert.Equal(3, _handler.Requests.Count);
    }

    [Fact]
    public async Task Gives_up_after_max_retries_when_nothing_is_cached()
    {
        _handler
            .Respond(HttpStatusCode.ServiceUnavailable)
            .Respond(HttpStatusCode.ServiceUnavailable)
            .Respond(HttpStatusCode.ServiceUnavailable);

        await Assert.ThrowsAsync<UpstreamUnavailableException>(() => _client.GetJsonAsync<Payload>("down", TimeSpan.FromMinutes(1)));
        Assert.Equal(3, _handler.Requests.Count);
    }

    [Fact]
    public async Task Serves_stale_copy_when_the_upstream_fails()
    {
        _handler
            .Respond(HttpStatusCode.OK, """{"value":9}""")
            .Throw(new HttpRequestException("connection reset"))
            .Respond(HttpStatusCode.ServiceUnavailable)
            .Respond(HttpStatusCode.ServiceUnavailable);

        await _client.GetJsonAsync<Payload>("thing", TimeSpan.FromMinutes(1));
        _clock.Advance(TimeSpan.FromMinutes(2));
        var stale = await _client.GetJsonAsync<Payload>("thing", TimeSpan.FromMinutes(1));

        Assert.Equal(9, stale.Value);
    }

    [Fact]
    public async Task Not_found_is_not_retried()
    {
        _handler.Respond(HttpStatusCode.NotFound);

        await Assert.ThrowsAsync<NotFoundException>(() => _client.GetJsonAsync<Payload>("missing", TimeSpan.FromMinutes(1)));
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task Unexpected_payload_shape_is_reported_as_upstream_error()
    {
        _handler.Respond(HttpStatusCode.OK, "<html>maintenance</html>");

        await Assert.ThrowsAsync<UpstreamUnavailableException>(() => _client.GetJsonAsync<Payload>("weird", TimeSpan.FromMinutes(1)));
    }

    public void Dispose()
    {
        _http.Dispose();
        _handler.Dispose();
        _cache.Dispose();
        _limiter.Dispose();
    }
}
