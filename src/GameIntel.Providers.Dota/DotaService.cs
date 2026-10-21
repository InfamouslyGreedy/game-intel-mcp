using GameIntel.Core;
using GameIntel.Core.Text;
using GameIntel.Providers.Dota.Analysis;
using GameIntel.Providers.Dota.Models;

namespace GameIntel.Providers.Dota;

/// <summary>Use cases behind the Dota 2 MCP tools: fetch, resolve names, delegate to <see cref="DotaAnalyzer"/>.</summary>
public sealed class DotaService(IOpenDotaClient client)
{
    // Common community shorthands that neither prefix nor initials matching would catch.
    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Anti-Mage"] = ["am", "antimage"],
        ["Shadow Fiend"] = ["sf", "nevermore"],
        ["Queen of Pain"] = ["qop"],
        ["Outworld Destroyer"] = ["od", "outworld devourer"],
        ["Nature's Prophet"] = ["np", "furion"],
        ["Windranger"] = ["wr", "windrunner"],
        ["Io"] = ["wisp"],
        ["Clockwerk"] = ["clock", "rattletrap"],
        ["Necrophos"] = ["necro", "necrolyte"],
        ["Zeus"] = ["zuus"],
        ["Lifestealer"] = ["naix", "ls"],
        ["Vengeful Spirit"] = ["venge", "vs"],
        ["Wraith King"] = ["wk", "skeleton king"],
        ["Faceless Void"] = ["void", "fv"],
        ["Phantom Assassin"] = ["pa", "mortred"],
        ["Templar Assassin"] = ["ta", "lanaya"],
    };

    public async Task<PlayerSummary> GetPlayerSummaryAsync(long steamId, int matches, CancellationToken cancellationToken)
    {
        var accountId = DotaAnalyzer.ToAccountId(steamId);
        if (accountId <= 0)
        {
            throw new GameIntelException("Account id must be a positive Steam32 or Steam64 id.");
        }

        var playerTask = client.GetPlayerAsync(accountId, cancellationToken);
        var matchesTask = client.GetRecentMatchesAsync(accountId, cancellationToken);
        var heroesTask = client.GetHeroesAsync(cancellationToken);
        await Task.WhenAll(playerTask, matchesTask, heroesTask).ConfigureAwait(false);

        var player = await playerTask.ConfigureAwait(false);
        var recent = await matchesTask.ConfigureAwait(false);

        if (player.Profile is null && recent.Count == 0)
        {
            throw new NotFoundException(
                $"OpenDota has no public data for account {accountId}. The profile may be private: in Dota 2, " +
                "enable Settings → Options → Social → 'Expose Public Match Data'.");
        }

        var names = (await heroesTask.ConfigureAwait(false)).ToDictionary(h => h.Id, h => h.LocalizedName);
        return DotaAnalyzer.Summarize(accountId, player, recent, names, matches);
    }

    public async Task<MatchupReport> GetMatchupsAsync(string heroQuery, int top, int minGames, CancellationToken cancellationToken)
    {
        var heroes = await client.GetHeroesAsync(cancellationToken).ConfigureAwait(false);
        var hero = ResolveHero(heroes, heroQuery);
        var matchups = await client.GetHeroMatchupsAsync(hero.Id, cancellationToken).ConfigureAwait(false);
        return DotaAnalyzer.Matchups(hero, matchups, heroes.ToDictionary(h => h.Id, h => h.LocalizedName), top, minGames);
    }

    public async Task<ItemBuild> GetItemBuildAsync(string heroQuery, int perPhase, CancellationToken cancellationToken)
    {
        var heroes = await client.GetHeroesAsync(cancellationToken).ConfigureAwait(false);
        var hero = ResolveHero(heroes, heroQuery);

        var popularityTask = client.GetItemPopularityAsync(hero.Id, cancellationToken);
        var itemsTask = client.GetItemConstantsAsync(cancellationToken);
        await Task.WhenAll(popularityTask, itemsTask).ConfigureAwait(false);

        return DotaAnalyzer.Items(hero, await popularityTask.ConfigureAwait(false), await itemsTask.ConfigureAwait(false), perPhase);
    }

    public async Task<IReadOnlyList<Hero>> ListHeroesAsync(CancellationToken cancellationToken) =>
        (await client.GetHeroesAsync(cancellationToken).ConfigureAwait(false))
            .OrderBy(h => h.LocalizedName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    internal static Hero ResolveHero(IReadOnlyList<Hero> heroes, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new GameIntelException("Hero name is required.");
        }

        var hero = NameMatcher.FindBest(
            heroes,
            query,
            h => h.LocalizedName,
            h => Aliases.TryGetValue(h.LocalizedName, out var a)
                ? a.Append(h.Name.Replace("npc_dota_hero_", string.Empty, StringComparison.Ordinal))
                : [h.Name.Replace("npc_dota_hero_", string.Empty, StringComparison.Ordinal)]);

        return hero ?? throw new NotFoundException(
            $"No Dota 2 hero matches '{query}'.",
            NameMatcher.Suggest(heroes, query, h => h.LocalizedName));
    }
}
