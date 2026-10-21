namespace GameIntel.Providers.Poe2.Models;

public sealed record League(string Id, string Name);

/// <summary>
/// One category of the PoE 2 currency exchange at a point in time, normalized from poe.ninja's
/// raw response. Values are in the league's primary reference currency.
/// </summary>
public sealed record MarketSnapshot(
    string League,
    string Category,
    Currency Primary,
    Currency? Secondary,
    double? SecondaryPerPrimary,
    IReadOnlyList<MarketEntry> Entries);

public sealed record Currency(string Id, string Name);

public sealed record MarketEntry(
    string Id,
    string Name,
    double? Value,
    double? Volume,
    double? ChangePercent,
    string? MostTradedAgainst);

/// <summary>The exchange categories poe.ninja publishes for PoE 2.</summary>
public static class Poe2Categories
{
    public static IReadOnlyList<string> All { get; } =
    [
        "Currency", "Fragments", "Abyss", "UncutGems", "LineageSupportGems", "Essences", "SoulCores",
        "Idols", "Runes", "Ritual", "Expedition", "Delirium", "Breach", "Verisium",
    ];

    /// <summary>Case- and spacing-insensitive: "soul cores" → "SoulCores".</summary>
    public static string? Parse(string? value)
    {
        var normalized = Core.Text.NameMatcher.Normalize(value);
        return All.FirstOrDefault(c => Core.Text.NameMatcher.Normalize(c) == normalized);
    }
}
