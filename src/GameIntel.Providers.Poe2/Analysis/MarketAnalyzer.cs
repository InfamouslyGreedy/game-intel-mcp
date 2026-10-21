using GameIntel.Core.Text;
using GameIntel.Providers.Poe2.Models;

namespace GameIntel.Providers.Poe2.Analysis;

/// <summary>Pure pricing logic over a <see cref="MarketSnapshot"/>.</summary>
public static class MarketAnalyzer
{
    public const string Attribution = "Prices from poe.ninja's PoE 2 exchange data, which refreshes about hourly.";

    public static MarketEntry? Find(MarketSnapshot snapshot, string query)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return NameMatcher.FindBest(snapshot.Entries, query, e => e.Name, e => [e.Id]);
    }

    public static PriceCheck ToPriceCheck(MarketSnapshot snapshot, MarketEntry entry)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(entry);

        double? inSecondary = entry.Value is { } v && snapshot.SecondaryPerPrimary is { } rate
            ? Round(v * rate)
            : null;

        return new PriceCheck(
            snapshot.League,
            snapshot.Category,
            entry.Name,
            entry.Value is { } value ? Round(value) : null,
            snapshot.Primary.Name,
            inSecondary,
            snapshot.Secondary?.Name,
            entry.ChangePercent is { } c ? Math.Round(c, 1) : null,
            entry.Volume is { } vol ? Math.Round(vol, 0) : null,
            entry.MostTradedAgainst,
            Attribution);
    }

    public static MarketList MostValuable(MarketSnapshot snapshot, int top)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var items = snapshot.Entries
            .Where(e => e.Value is > 0)
            .OrderByDescending(e => e.Value)
            .Take(Math.Clamp(top, 1, 50))
            .Select(ToListEntry)
            .ToList();

        return new MarketList(snapshot.League, snapshot.Category, snapshot.Primary.Name, items, Attribution);
    }

    /// <summary>
    /// Biggest price swings in either direction. Entries with tiny volume are excluded because a
    /// single trade can move an illiquid item by hundreds of percent.
    /// </summary>
    public static MarketList Movers(MarketSnapshot snapshot, int top, double minVolume)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var withTrend = snapshot.Entries.Where(e => e.ChangePercent is not null).ToList();
        var items = withTrend
            .Where(e => (e.Volume ?? 0) >= minVolume)
            .OrderByDescending(e => Math.Abs(e.ChangePercent!.Value))
            .Take(Math.Clamp(top, 1, 50))
            .Select(ToListEntry)
            .ToList();

        var note = withTrend.Count == 0
            ? "poe.ninja did not include trend data for this category, so movers can't be computed. " + Attribution
            : $"Ranked by absolute price change; items under {minVolume:0} volume are excluded. " + Attribution;

        return new MarketList(snapshot.League, snapshot.Category, snapshot.Primary.Name, items, note);
    }

    private static MarketListEntry ToListEntry(MarketEntry e) => new(
        e.Name,
        e.Value is { } v ? Round(v) : null,
        e.ChangePercent is { } c ? Math.Round(c, 1) : null,
        e.Volume is { } vol ? Math.Round(vol, 0) : null);

    /// <summary>Keep cheap items readable: 0.00042 stays meaningful, 1234.5678 becomes 1234.57.</summary>
    internal static double Round(double value) =>
        Math.Abs(value) >= 1 ? Math.Round(value, 2) : Math.Round(value, 5);
}
