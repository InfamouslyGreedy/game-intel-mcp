using System.Text.Json;
using GameIntel.Core.Http;
using GameIntel.Providers.Poe2.Analysis;
using GameIntel.Providers.Poe2.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameIntel.Providers.Poe2;

public interface IPoeNinjaClient
{
    Task<IReadOnlyList<League>> GetLeaguesAsync(CancellationToken cancellationToken = default);

    Task<MarketSnapshot> GetExchangeAsync(string league, string category, CancellationToken cancellationToken = default);
}

/// <summary>
/// Client for poe.ninja's supported PoE 2 economy endpoints. Only the economy routes are used:
/// poe.ninja documents its builds/character routes as internal, so this project stays off them.
/// </summary>
public sealed class PoeNinjaClient : IPoeNinjaClient
{
    // PoE 2 exchange data refreshes roughly hourly; poe.ninja asks clients not to poll faster than
    // every few minutes. 15 minutes keeps answers current without wasting their bandwidth.
    private static readonly TimeSpan ExchangeFreshness = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan LeagueFreshness = TimeSpan.FromHours(1);

    private readonly ResilientApiClient _api;

    public PoeNinjaClient(
        HttpClient http,
        IMemoryCache cache,
        ProviderRateLimiter<PoeNinjaClient> limiter,
        IOptionsMonitor<ApiClientOptions> options,
        TimeProvider time,
        ILogger<PoeNinjaClient> logger)
    {
        ArgumentNullException.ThrowIfNull(limiter);
        ArgumentNullException.ThrowIfNull(options);

        _api = new ResilientApiClient(
            "poeninja",
            http,
            cache,
            limiter.Limiter,
            options.Get(Poe2Provider.ProviderId),
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            time,
            logger);
    }

    public async Task<IReadOnlyList<League>> GetLeaguesAsync(CancellationToken cancellationToken = default)
    {
        var root = await _api.GetJsonAsync<JsonElement>("poe2/api/economy/leagues", LeagueFreshness, cancellationToken)
            .ConfigureAwait(false);
        return ExchangeParser.ParseLeagues(root);
    }

    public async Task<MarketSnapshot> GetExchangeAsync(string league, string category, CancellationToken cancellationToken = default)
    {
        var url = $"poe2/api/economy/exchange/current/overview?league={Uri.EscapeDataString(league)}&type={Uri.EscapeDataString(category)}";
        var root = await _api.GetJsonAsync<JsonElement>(url, ExchangeFreshness, cancellationToken).ConfigureAwait(false);
        return ExchangeParser.Parse(root, league, category);
    }
}
