using GameIntel.Providers.Dota.Models;

namespace GameIntel.Providers.Dota.Analysis;

/// <summary>
/// Pure functions over OpenDota data. No I/O here, so every rule is unit-testable
/// with hand-written fixtures.
/// </summary>
public static class DotaAnalyzer
{
    /// <summary>Steam64 ids start here; OpenDota wants the 32-bit account id.</summary>
    public const long Steam64Offset = 76561197960265728;

    private static readonly string[] Medals =
        ["Herald", "Guardian", "Crusader", "Archon", "Legend", "Ancient", "Divine", "Immortal"];

    /// <summary>Accepts either a Steam32 account id or a Steam64 id and returns the account id.</summary>
    public static long ToAccountId(long steamId) => steamId >= Steam64Offset ? steamId - Steam64Offset : steamId;

    /// <summary>
    /// OpenDota's rank_tier packs medal and stars into two digits: 54 → "Legend 4", 80 → "Immortal".
    /// </summary>
    public static string? DescribeRank(int? rankTier, int? leaderboardRank = null)
    {
        if (rankTier is not { } tier || tier <= 0)
        {
            return null;
        }

        var medal = tier / 10;
        var stars = tier % 10;
        if (medal < 1 || medal > Medals.Length)
        {
            return null;
        }

        if (medal == Medals.Length)
        {
            return leaderboardRank is > 0 ? $"Immortal (rank {leaderboardRank})" : "Immortal";
        }

        return stars is >= 1 and <= 5 ? $"{Medals[medal - 1]} {stars}" : Medals[medal - 1];
    }

    /// <summary>Player slots 0–127 are Radiant, 128+ are Dire.</summary>
    public static bool IsRadiant(int playerSlot) => playerSlot < 128;

    public static bool Won(RecentMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);
        return IsRadiant(match.PlayerSlot) == match.RadiantWin;
    }

    public static PlayerSummary Summarize(
        long accountId,
        PlayerResponse? player,
        IReadOnlyList<RecentMatch> matches,
        IReadOnlyDictionary<int, string> heroNames,
        int maxMatches = 20,
        int topHeroes = 3)
    {
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentNullException.ThrowIfNull(heroNames);

        var sample = matches.OrderByDescending(m => m.StartTime).Take(Math.Clamp(maxMatches, 1, 20)).ToList();
        var count = sample.Count;
        var wins = sample.Count(Won);

        double Avg(Func<RecentMatch, double> f) => count == 0 ? 0 : Math.Round(sample.Average(f), 1);
        double? AvgNullable(Func<RecentMatch, int?> f)
        {
            var values = sample.Select(f).Where(v => v.HasValue).Select(v => (double)v!.Value).ToList();
            return values.Count == 0 ? null : Math.Round(values.Average(), 0);
        }

        var kills = sample.Sum(m => m.Kills);
        var deaths = sample.Sum(m => m.Deaths);
        var assists = sample.Sum(m => m.Assists);

        string Name(int heroId) => heroNames.TryGetValue(heroId, out var n) ? n : $"Hero #{heroId}";

        var heroes = sample
            .GroupBy(m => m.HeroId)
            .Select(g => new HeroUsage(Name(g.Key), g.Count(), g.Count(Won), Percent(g.Count(Won), g.Count())))
            .OrderByDescending(h => h.Games)
            .ThenByDescending(h => h.WinRatePercent)
            .Take(topHeroes)
            .ToList();

        var lines = sample
            .Select(m => new MatchLine(
                m.MatchId,
                Name(m.HeroId),
                Won(m),
                IsRadiant(m.PlayerSlot) ? "Radiant" : "Dire",
                $"{m.Kills}/{m.Deaths}/{m.Assists}",
                (int)Math.Round(m.Duration / 60.0),
                DateTimeOffset.FromUnixTimeSeconds(m.StartTime)))
            .ToList();

        return new PlayerSummary(
            accountId,
            player?.Profile?.Personaname,
            DescribeRank(player?.RankTier, player?.LeaderboardRank),
            count,
            wins,
            count - wins,
            Percent(wins, count),
            Avg(m => m.Kills),
            Avg(m => m.Deaths),
            Avg(m => m.Assists),
            Math.Round((kills + assists) / (double)Math.Max(1, deaths), 2),
            AvgNullable(m => m.GoldPerMin),
            AvgNullable(m => m.XpPerMin),
            Streak(sample),
            heroes,
            lines);
    }

    /// <summary>"W3" = won the last three, "L2" = lost the last two, "-" = no games.</summary>
    public static string Streak(IReadOnlyList<RecentMatch> newestFirst)
    {
        ArgumentNullException.ThrowIfNull(newestFirst);
        if (newestFirst.Count == 0)
        {
            return "-";
        }

        var first = Won(newestFirst[0]);
        var length = newestFirst.TakeWhile(m => Won(m) == first).Count();
        return $"{(first ? 'W' : 'L')}{length}";
    }

    /// <summary>
    /// Splits matchups into best and worst opponents. Win rates are from the perspective of
    /// <paramref name="hero"/>. Low-sample matchups are dropped because 3-0 is noise, not insight.
    /// </summary>
    public static MatchupReport Matchups(
        Hero hero,
        IReadOnlyList<HeroMatchup> matchups,
        IReadOnlyDictionary<int, string> heroNames,
        int top = 5,
        int minGames = 50)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(matchups);
        ArgumentNullException.ThrowIfNull(heroNames);

        var eligible = matchups
            .Where(m => m.GamesPlayed >= minGames && m.HeroId != hero.Id)
            .Select(m => new MatchupLine(
                heroNames.TryGetValue(m.HeroId, out var n) ? n : $"Hero #{m.HeroId}",
                m.GamesPlayed,
                Percent(m.Wins, m.GamesPlayed)))
            .ToList();

        var strong = eligible.OrderByDescending(m => m.WinRatePercent).ThenByDescending(m => m.Games).Take(top).ToList();
        var weak = eligible.OrderBy(m => m.WinRatePercent).ThenByDescending(m => m.Games).Take(top).ToList();

        var note = eligible.Count == 0
            ? $"No opponent has at least {minGames} recorded games against {hero.LocalizedName}; try a lower minGames."
            : "Win rates come from OpenDota's sample of public matches and are from this hero's perspective.";

        return new MatchupReport(hero.LocalizedName, minGames, strong, weak, note);
    }

    /// <summary>Turns per-phase purchase counts keyed by item id into named, ranked picks.</summary>
    public static ItemBuild Items(
        Hero hero,
        ItemPopularity popularity,
        IReadOnlyDictionary<string, ItemConstant> itemConstants,
        int perPhase = 6)
    {
        ArgumentNullException.ThrowIfNull(hero);
        ArgumentNullException.ThrowIfNull(popularity);
        ArgumentNullException.ThrowIfNull(itemConstants);

        var byId = itemConstants.Values
            .GroupBy(i => i.Id)
            .ToDictionary(g => g.Key.ToString(System.Globalization.CultureInfo.InvariantCulture), g => g.First());

        ItemPhase Phase(string label, IReadOnlyDictionary<string, int>? counts) => new(
            label,
            (counts ?? new Dictionary<string, int>())
                .OrderByDescending(kv => kv.Value)
                .Take(perPhase)
                .Select(kv => byId.TryGetValue(kv.Key, out var item)
                    ? new ItemPick(item.Dname ?? $"Item #{kv.Key}", kv.Value, item.Cost)
                    : new ItemPick($"Item #{kv.Key}", kv.Value, null))
                .ToList());

        return new ItemBuild(
            hero.LocalizedName,
            [
                Phase("Starting items", popularity.StartGameItems),
                Phase("Early game (0–10 min)", popularity.EarlyGameItems),
                Phase("Mid game (10–25 min)", popularity.MidGameItems),
                Phase("Late game (25+ min)", popularity.LateGameItems),
            ],
            "Ranked by purchase count in OpenDota's recent public-match sample, not by win rate.");
    }

    internal static double Percent(int part, int whole) =>
        whole == 0 ? 0 : Math.Round(100.0 * part / whole, 1);
}
