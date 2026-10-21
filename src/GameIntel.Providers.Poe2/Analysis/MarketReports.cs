namespace GameIntel.Providers.Poe2.Analysis;

public sealed record PriceCheck(
    string League,
    string Category,
    string Item,
    double? Value,
    string ValueCurrency,
    double? ValueInSecondary,
    string? SecondaryCurrency,
    double? ChangePercent,
    double? Volume,
    string? MostTradedAgainst,
    string Note);

public sealed record MarketList(
    string League,
    string Category,
    string ValueCurrency,
    IReadOnlyList<MarketListEntry> Items,
    string Note);

public sealed record MarketListEntry(string Item, double? Value, double? ChangePercent, double? Volume);
