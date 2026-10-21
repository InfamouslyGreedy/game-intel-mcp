using System.ComponentModel;
using GameIntel.Providers.Poe2;
using ModelContextProtocol.Server;

namespace GameIntel.McpServer.Tools;

/// <summary>Path of Exile 2 economy tools backed by poe.ninja.</summary>
[McpServerToolType]
public sealed class Poe2Tools(Poe2Service poe, ToolRunner runner)
{
    private const string CategoryHelp =
        "Exchange category: Currency, Fragments, Abyss, UncutGems, LineageSupportGems, Essences, SoulCores, Idols, Runes, Ritual, Expedition, Delirium, Breach or Verisium.";

    private const string LeagueHelp =
        "League name or id. Omit for the current challenge league.";

    [McpServerTool(Name = "poe2_leagues", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Lists Path of Exile 2 leagues that have economy data. The first entry is the current challenge league.")]
    public Task<string> Leagues(CancellationToken cancellationToken = default) =>
        runner.RunAsync("poe2_leagues", () => poe.GetLeaguesAsync(cancellationToken));

    [McpServerTool(Name = "poe2_price_check", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Gets the current Path of Exile 2 exchange price of an item (currency, runes, essences, soul cores, fragments, omens, etc.), in the league's primary and secondary reference currencies, with trend and trade volume.")]
    public Task<string> PriceCheck(
        [Description("Item name; partial and lowercase names work, e.g. 'divine', 'exalted orb', 'greater rune of'.")] string item,
        [Description(LeagueHelp)] string? league = null,
        [Description(CategoryHelp + " Omit to search all categories.")] string? category = null,
        CancellationToken cancellationToken = default) =>
        runner.RunAsync("poe2_price_check", () => poe.PriceCheckAsync(item, league, category, cancellationToken));

    [McpServerTool(Name = "poe2_most_valuable", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Lists the most valuable items in a Path of Exile 2 exchange category. Good for 'what should I be farming' or 'what is worth picking up' questions.")]
    public Task<string> MostValuable(
        [Description(CategoryHelp)] string category = "Currency",
        [Description(LeagueHelp)] string? league = null,
        [Description("How many items to return, 1-50. Default 10.")] int top = 10,
        CancellationToken cancellationToken = default) =>
        runner.RunAsync("poe2_most_valuable", () => poe.MostValuableAsync(category, league, top, cancellationToken));

    [McpServerTool(Name = "poe2_market_movers", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Lists Path of Exile 2 items whose price changed the most recently, up or down. Illiquid items are filtered out by minimum trade volume.")]
    public Task<string> MarketMovers(
        [Description(CategoryHelp)] string category = "Currency",
        [Description(LeagueHelp)] string? league = null,
        [Description("How many items to return, 1-50. Default 10.")] int top = 10,
        [Description("Minimum trade volume (in primary currency) to include an item. Default 10.")] double minVolume = 10,
        CancellationToken cancellationToken = default) =>
        runner.RunAsync("poe2_market_movers", () => poe.MoversAsync(category, league, top, minVolume, cancellationToken));
}
