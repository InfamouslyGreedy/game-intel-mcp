namespace GameIntel.Providers.Dota.Analysis;

// Shapes returned to the agent. They are deliberately flat, pre-computed and labelled
// (percentages already rounded, heroes already named) so the model spends its effort on advice,
// not on arithmetic over raw ids.

public sealed record PlayerSummary(
    long AccountId,
    string? PlayerName,
    string? Rank,
    int MatchesAnalyzed,
    int Wins,
    int Losses,
    double WinRatePercent,
    double AverageKills,
    double AverageDeaths,
    double AverageAssists,
    double Kda,
    double? AverageGpm,
    double? AverageXpm,
    string CurrentStreak,
    IReadOnlyList<HeroUsage> MostPlayedHeroes,
    IReadOnlyList<MatchLine> RecentMatches);

public sealed record HeroUsage(string Hero, int Games, int Wins, double WinRatePercent);

public sealed record MatchLine(
    long MatchId,
    string Hero,
    bool Won,
    string Side,
    string Kda,
    int DurationMinutes,
    DateTimeOffset PlayedAt);

public sealed record MatchupReport(
    string Hero,
    int MinimumGames,
    IReadOnlyList<MatchupLine> StrongAgainst,
    IReadOnlyList<MatchupLine> WeakAgainst,
    string Note);

public sealed record MatchupLine(string Opponent, int Games, double WinRatePercent);

public sealed record ItemBuild(string Hero, IReadOnlyList<ItemPhase> Phases, string Note);

public sealed record ItemPhase(string Phase, IReadOnlyList<ItemPick> Items);

public sealed record ItemPick(string Item, int Purchases, int? Cost);
