using GameIntel.Core;
using GameIntel.Core.Text;
using GameIntel.Providers.Poe2.Analysis;
using GameIntel.Providers.Poe2.Models;

namespace GameIntel.Providers.Poe2;

/// <summary>Use cases behind the PoE 2 MCP tools.</summary>
public sealed class Poe2Service(IPoeNinjaClient client)
{
    /// <summary>
    /// Order to search when the caller doesn't name a category. Most price checks are for basic
    /// currency, so this usually resolves on the first (cached) request.
    /// </summary>
    private static readonly string[] SearchOrder =
    [
        "Currency", "Fragments", "Runes", "Essences", "SoulCores", "Ritual", "Delirium", "Breach",
        "Expedition", "Abyss", "Idols", "UncutGems", "LineageSupportGems", "Verisium",
    ];

    public Task<IReadOnlyList<League>> GetLeaguesAsync(CancellationToken cancellationToken) =>
        client.GetLeaguesAsync(cancellationToken);

    public async Task<PriceCheck> PriceCheckAsync(string itemQuery, string? league, string? category, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(itemQuery))
        {
            throw new GameIntelException("Item name is required.");
        }

        var leagueId = await ResolveLeagueAsync(league, cancellationToken).ConfigureAwait(false);
        var categories = category is null ? SearchOrder : [RequireCategory(category)];

        var seen = new List<MarketEntry>();
        foreach (var c in categories)
        {
            var snapshot = await client.GetExchangeAsync(leagueId, c, cancellationToken).ConfigureAwait(false);
            if (MarketAnalyzer.Find(snapshot, itemQuery) is { } entry)
            {
                return MarketAnalyzer.ToPriceCheck(snapshot, entry);
            }

            seen.AddRange(snapshot.Entries);
        }

        throw new NotFoundException(
            $"No exchange listing matches '{itemQuery}' in {leagueId}.",
            NameMatcher.Suggest(seen, itemQuery, e => e.Name));
    }

    public async Task<MarketList> MostValuableAsync(string category, string? league, int top, CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotAsync(category, league, cancellationToken).ConfigureAwait(false);
        return MarketAnalyzer.MostValuable(snapshot, top);
    }

    public async Task<MarketList> MoversAsync(string category, string? league, int top, double minVolume, CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotAsync(category, league, cancellationToken).ConfigureAwait(false);
        return MarketAnalyzer.Movers(snapshot, top, minVolume);
    }

    private async Task<MarketSnapshot> SnapshotAsync(string category, string? league, CancellationToken cancellationToken)
    {
        var leagueId = await ResolveLeagueAsync(league, cancellationToken).ConfigureAwait(false);
        return await client.GetExchangeAsync(leagueId, RequireCategory(category), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Null means "current league": poe.ninja lists the active challenge league first.
    /// Otherwise accept a league id or display name, forgivingly.
    /// </summary>
    internal async Task<string> ResolveLeagueAsync(string? league, CancellationToken cancellationToken)
    {
        var leagues = await client.GetLeaguesAsync(cancellationToken).ConfigureAwait(false);
        if (leagues.Count == 0)
        {
            throw new UpstreamUnavailableException("poe.ninja returned no PoE 2 leagues.");
        }

        if (string.IsNullOrWhiteSpace(league))
        {
            return leagues[0].Id;
        }

        var match = NameMatcher.FindBest(leagues, league, l => l.Name, l => [l.Id]);
        return match?.Id ?? throw new NotFoundException(
            $"No PoE 2 league matches '{league}'.",
            leagues.Select(l => l.Name).ToList());
    }

    private static string RequireCategory(string category) =>
        Poe2Categories.Parse(category) ?? throw new GameIntelException(
            $"Unknown category '{category}'. Valid categories: {string.Join(", ", Poe2Categories.All)}.");
}
