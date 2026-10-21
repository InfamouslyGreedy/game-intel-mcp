using System.ComponentModel;
using GameIntel.Providers.Dota;
using ModelContextProtocol.Server;

namespace GameIntel.McpServer.Tools;

/// <summary>
/// Dota 2 tools. Thin adapters only: validate and clamp inputs, call <see cref="DotaService"/>,
/// let <see cref="ToolRunner"/> shape the output. Descriptions are written for the model,
/// since they are how it decides which tool to call and with what arguments.
/// </summary>
[McpServerToolType]
public sealed class DotaTools(DotaService dota, ToolRunner runner)
{
    [McpServerTool(Name = "dota2_player_summary", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Summarizes a Dota 2 player's recent form from OpenDota: rank medal, win/loss, win rate, average K/D/A and KDA ratio, GPM/XPM, current streak, most-played heroes, and a per-match list. Requires the player's match data to be public.")]
    public Task<string> PlayerSummary(
        [Description("Steam account id: the 32-bit Dota/OpenDota id (e.g. 86745912) or a 64-bit Steam id (7656119...). Both work.")] long accountId,
        [Description("How many recent matches to analyze, 1-20. Default 20.")] int matches = 20,
        CancellationToken cancellationToken = default) =>
        runner.RunAsync("dota2_player_summary", () =>
            dota.GetPlayerSummaryAsync(accountId, Math.Clamp(matches, 1, 20), cancellationToken));

    [McpServerTool(Name = "dota2_hero_matchups", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Shows which heroes a Dota 2 hero does best and worst against, by win rate in public matches. Use for counter-picking and drafting questions like 'what counters Phantom Assassin?' (look at that hero's weakAgainst list).")]
    public Task<string> HeroMatchups(
        [Description("Hero name. Forgiving: 'am', 'anti mage' and 'Anti-Mage' all work.")] string hero,
        [Description("How many heroes to list on each side, 1-15. Default 5.")] int top = 5,
        [Description("Ignore matchups with fewer recorded games than this, to avoid noisy win rates. Default 50.")] int minGames = 50,
        CancellationToken cancellationToken = default) =>
        runner.RunAsync("dota2_hero_matchups", () =>
            dota.GetMatchupsAsync(hero, Math.Clamp(top, 1, 15), Math.Max(1, minGames), cancellationToken));

    [McpServerTool(Name = "dota2_item_build", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Lists the most purchased items for a Dota 2 hero in each game phase (starting, early, mid, late) with item costs. Reflects what players buy, not proven win rates.")]
    public Task<string> ItemBuild(
        [Description("Hero name, e.g. 'Juggernaut' or 'jugg'.")] string hero,
        [Description("Items to list per phase, 1-12. Default 6.")] int perPhase = 6,
        CancellationToken cancellationToken = default) =>
        runner.RunAsync("dota2_item_build", () =>
            dota.GetItemBuildAsync(hero, Math.Clamp(perPhase, 1, 12), cancellationToken));

    [McpServerTool(Name = "dota2_list_heroes", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Lists every Dota 2 hero with primary attribute and roles. Useful to resolve a name or to filter heroes by role (e.g. all Supports).")]
    public Task<string> ListHeroes(CancellationToken cancellationToken = default) =>
        runner.RunAsync("dota2_list_heroes", async () =>
            (await dota.ListHeroesAsync(cancellationToken).ConfigureAwait(false))
                .Select(h => new { h.LocalizedName, h.PrimaryAttr, h.Roles }));
}
