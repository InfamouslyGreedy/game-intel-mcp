using GameIntel.Providers.Dota.Analysis;
using GameIntel.Providers.Dota.Models;

namespace GameIntel.Tests.Dota;

public sealed class DotaAnalyzerTests
{
    private static readonly Dictionary<int, string> Names = new() { [1] = "Anti-Mage", [8] = "Juggernaut", [44] = "Phantom Assassin" };

    private static RecentMatch Match(long id, int slot, bool radiantWin, int hero = 1, int k = 5, int d = 5, int a = 5, long start = 0) =>
        new(id, slot, radiantWin, 2400, hero, k, d, a, 500, 600, 150, start);

    [Theory]
    [InlineData(54, null, "Legend 4")]
    [InlineData(11, null, "Herald 1")]
    [InlineData(70, null, "Divine")]
    [InlineData(80, null, "Immortal")]
    [InlineData(80, 1234, "Immortal (rank 1234)")]
    public void Describes_rank_tier(int tier, int? leaderboard, string expected)
    {
        Assert.Equal(expected, DotaAnalyzer.DescribeRank(tier, leaderboard));
    }

    [Fact]
    public void Unranked_or_invalid_tier_has_no_rank()
    {
        Assert.Null(DotaAnalyzer.DescribeRank(null));
        Assert.Null(DotaAnalyzer.DescribeRank(0));
        Assert.Null(DotaAnalyzer.DescribeRank(95));
    }

    [Theory]
    [InlineData(86745912L, 86745912L)]
    [InlineData(76561198047011640L, 86745912L)]
    public void Accepts_steam32_and_steam64_ids(long input, long expected)
    {
        Assert.Equal(expected, DotaAnalyzer.ToAccountId(input));
    }

    [Theory]
    [InlineData(0, true, true)]     // Radiant slot, Radiant won
    [InlineData(4, false, false)]   // Radiant slot, Dire won
    [InlineData(128, false, true)]  // Dire slot, Dire won
    [InlineData(132, true, false)]  // Dire slot, Radiant won
    public void Determines_win_from_slot_and_result(int slot, bool radiantWin, bool expected)
    {
        Assert.Equal(expected, DotaAnalyzer.Won(Match(1, slot, radiantWin)));
    }

    [Fact]
    public void Summarizes_recent_form()
    {
        var matches = new[]
        {
            Match(3, 0, true, hero: 1, k: 10, d: 2, a: 8, start: 300),   // win
            Match(2, 128, false, hero: 1, k: 6, d: 4, a: 10, start: 200), // win
            Match(1, 0, false, hero: 8, k: 2, d: 9, a: 3, start: 100),   // loss
        };
        var player = new PlayerResponse(new PlayerProfile(42, "Omkar", null, null), 54, null);

        var summary = DotaAnalyzer.Summarize(42, player, matches, Names);

        Assert.Equal("Omkar", summary.PlayerName);
        Assert.Equal("Legend 4", summary.Rank);
        Assert.Equal(3, summary.MatchesAnalyzed);
        Assert.Equal(2, summary.Wins);
        Assert.Equal(66.7, summary.WinRatePercent);
        Assert.Equal("W2", summary.CurrentStreak);
        Assert.Equal(Math.Round((18 + 21) / 15.0, 2), summary.Kda);
        Assert.Equal("Anti-Mage", summary.MostPlayedHeroes[0].Hero);
        Assert.Equal(2, summary.MostPlayedHeroes[0].Games);
        Assert.Equal(3, summary.RecentMatches[0].MatchId);
        Assert.Equal("Dire", summary.RecentMatches[1].Side);
    }

    [Fact]
    public void Summary_of_no_matches_does_not_divide_by_zero()
    {
        var summary = DotaAnalyzer.Summarize(42, null, [], Names);

        Assert.Equal(0, summary.WinRatePercent);
        Assert.Equal("-", summary.CurrentStreak);
        Assert.Null(summary.AverageGpm);
    }

    [Fact]
    public void Matchups_drop_small_samples_and_rank_both_directions()
    {
        var am = new Hero(1, "npc_dota_hero_antimage", "Anti-Mage", "agi", ["Carry"]);
        var matchups = new[]
        {
            new HeroMatchup(8, 200, 120),  // 60%
            new HeroMatchup(44, 300, 120), // 40%
            new HeroMatchup(99, 10, 10),   // 100% but only 10 games: noise
        };

        var report = DotaAnalyzer.Matchups(am, matchups, Names, top: 5, minGames: 50);

        Assert.Equal("Juggernaut", report.StrongAgainst[0].Opponent);
        Assert.Equal(60, report.StrongAgainst[0].WinRatePercent);
        Assert.Equal("Phantom Assassin", report.WeakAgainst[0].Opponent);
        Assert.DoesNotContain(report.StrongAgainst, m => m.Games == 10);
    }

    [Fact]
    public void Item_build_names_items_and_orders_by_purchases()
    {
        var hero = new Hero(8, "npc_dota_hero_juggernaut", "Juggernaut", "agi", ["Carry"]);
        var popularity = new ItemPopularity(
            new Dictionary<string, int> { ["44"] = 50, ["16"] = 90 },
            null,
            new Dictionary<string, int> { ["63"] = 70, ["999"] = 5 },
            null);
        var items = new Dictionary<string, ItemConstant>
        {
            ["tango"] = new(44, "Tango", 90),
            ["branches"] = new(16, "Iron Branch", 50),
            ["power_treads"] = new(63, "Power Treads", 1400),
        };

        var build = DotaAnalyzer.Items(hero, popularity, items);

        Assert.Equal(4, build.Phases.Count);
        Assert.Equal("Iron Branch", build.Phases[0].Items[0].Item);
        Assert.Empty(build.Phases[1].Items);
        Assert.Equal("Power Treads", build.Phases[2].Items[0].Item);
        Assert.Equal("Item #999", build.Phases[2].Items[1].Item);
    }
}
