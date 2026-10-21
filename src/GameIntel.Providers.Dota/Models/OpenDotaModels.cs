namespace GameIntel.Providers.Dota.Models;

// Wire models for https://docs.opendota.com. OpenDota uses snake_case JSON; the client is configured
// with JsonNamingPolicy.SnakeCaseLower, so these PascalCase names map automatically.
// Anything OpenDota sometimes omits or nulls is declared nullable.

public sealed record PlayerResponse(PlayerProfile? Profile, int? RankTier, int? LeaderboardRank);

public sealed record PlayerProfile(long AccountId, string? Personaname, string? Avatarfull, string? Profileurl);

public sealed record RecentMatch(
    long MatchId,
    int PlayerSlot,
    bool RadiantWin,
    int Duration,
    int HeroId,
    int Kills,
    int Deaths,
    int Assists,
    int? GoldPerMin,
    int? XpPerMin,
    int? LastHits,
    long StartTime);

public sealed record Hero(int Id, string Name, string LocalizedName, string? PrimaryAttr, IReadOnlyList<string>? Roles);

public sealed record HeroMatchup(int HeroId, int GamesPlayed, int Wins);

/// <summary>Item purchase counts per game phase; keys are OpenDota item ids as strings.</summary>
public sealed record ItemPopularity(
    IReadOnlyDictionary<string, int>? StartGameItems,
    IReadOnlyDictionary<string, int>? EarlyGameItems,
    IReadOnlyDictionary<string, int>? MidGameItems,
    IReadOnlyDictionary<string, int>? LateGameItems);

/// <summary>One entry of /constants/items, which is keyed by internal item name.</summary>
public sealed record ItemConstant(int Id, string? Dname, int? Cost);
