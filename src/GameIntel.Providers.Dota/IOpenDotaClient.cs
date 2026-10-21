using GameIntel.Providers.Dota.Models;

namespace GameIntel.Providers.Dota;

/// <summary>Typed access to the OpenDota endpoints this provider uses.</summary>
public interface IOpenDotaClient
{
    Task<PlayerResponse> GetPlayerAsync(long accountId, CancellationToken cancellationToken = default);

    /// <summary>The player's last 20 matches (OpenDota's fixed page size for this endpoint).</summary>
    Task<IReadOnlyList<RecentMatch>> GetRecentMatchesAsync(long accountId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Hero>> GetHeroesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HeroMatchup>> GetHeroMatchupsAsync(int heroId, CancellationToken cancellationToken = default);

    Task<ItemPopularity> GetItemPopularityAsync(int heroId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, ItemConstant>> GetItemConstantsAsync(CancellationToken cancellationToken = default);
}
