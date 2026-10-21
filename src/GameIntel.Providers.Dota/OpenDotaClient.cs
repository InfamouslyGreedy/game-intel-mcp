using System.Globalization;
using System.Text.Json;
using GameIntel.Core.Http;
using GameIntel.Providers.Dota.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameIntel.Providers.Dota;

/// <summary>
/// OpenDota API client. Cache lifetimes reflect how often each dataset actually changes:
/// hero and item constants change with game patches, aggregate stats a few times a day,
/// and a player's match list whenever they finish a game.
/// </summary>
public sealed class OpenDotaClient : IOpenDotaClient
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static readonly TimeSpan Constants = TimeSpan.FromHours(24);
    private static readonly TimeSpan Aggregates = TimeSpan.FromHours(6);
    private static readonly TimeSpan Profile = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan Matches = TimeSpan.FromMinutes(2);

    private readonly ResilientApiClient _api;
    private readonly string? _apiKey;

    public OpenDotaClient(
        HttpClient http,
        IMemoryCache cache,
        ProviderRateLimiter<OpenDotaClient> limiter,
        IOptionsMonitor<ApiClientOptions> options,
        TimeProvider time,
        ILogger<OpenDotaClient> logger)
    {
        ArgumentNullException.ThrowIfNull(limiter);
        ArgumentNullException.ThrowIfNull(options);

        var settings = options.Get(DotaProvider.ProviderId);
        _apiKey = settings.ApiKey;
        _api = new ResilientApiClient("opendota", http, cache, limiter.Limiter, settings, Json, time, logger);
    }

    public Task<PlayerResponse> GetPlayerAsync(long accountId, CancellationToken cancellationToken = default) =>
        _api.GetJsonAsync<PlayerResponse>(Url($"players/{accountId}"), Profile, cancellationToken);

    public async Task<IReadOnlyList<RecentMatch>> GetRecentMatchesAsync(long accountId, CancellationToken cancellationToken = default) =>
        await _api.GetJsonAsync<List<RecentMatch>>(Url($"players/{accountId}/recentMatches"), Matches, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Hero>> GetHeroesAsync(CancellationToken cancellationToken = default) =>
        await _api.GetJsonAsync<List<Hero>>(Url("heroes"), Constants, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<HeroMatchup>> GetHeroMatchupsAsync(int heroId, CancellationToken cancellationToken = default) =>
        await _api.GetJsonAsync<List<HeroMatchup>>(Url($"heroes/{heroId}/matchups"), Aggregates, cancellationToken)
            .ConfigureAwait(false);

    public Task<ItemPopularity> GetItemPopularityAsync(int heroId, CancellationToken cancellationToken = default) =>
        _api.GetJsonAsync<ItemPopularity>(Url($"heroes/{heroId}/itemPopularity"), Aggregates, cancellationToken);

    public async Task<IReadOnlyDictionary<string, ItemConstant>> GetItemConstantsAsync(CancellationToken cancellationToken = default) =>
        await _api.GetJsonAsync<Dictionary<string, ItemConstant>>(Url("constants/items"), Constants, cancellationToken)
            .ConfigureAwait(false);

    // The key goes in the query string (OpenDota's convention). It is part of the cache key too,
    // which is fine because one process uses one key.
    private string Url(string path) =>
        string.IsNullOrWhiteSpace(_apiKey)
            ? path
            : string.Create(CultureInfo.InvariantCulture, $"{path}?api_key={Uri.EscapeDataString(_apiKey)}");
}
