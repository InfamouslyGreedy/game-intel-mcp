using GameIntel.Core;
using GameIntel.Core.Abstractions;

namespace GameIntel.Providers.Dota;

public sealed class DotaProvider(IOpenDotaClient client) : IGameProvider
{
    public const string ProviderId = "dota2";

    public string Id => ProviderId;

    public string DisplayName => "Dota 2";

    public string DataSource => "OpenDota public API (https://www.opendota.com)";

    public IReadOnlyList<string> Capabilities { get; } =
    [
        "Player summary: rank, win rate, KDA, GPM/XPM, most-played heroes, streak (last 20 matches)",
        "Hero matchups: best and worst opponents by win rate",
        "Item builds: most purchased items per game phase",
    ];

    public async Task<ProviderHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var heroes = await client.GetHeroesAsync(cancellationToken).ConfigureAwait(false);
            return heroes.Count > 0 ? ProviderHealth.Healthy : ProviderHealth.Unhealthy("Hero list was empty.");
        }
        catch (GameIntelException ex)
        {
            return ProviderHealth.Unhealthy(ex.Message);
        }
    }
}
