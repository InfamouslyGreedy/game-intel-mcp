using GameIntel.Core;
using GameIntel.Core.Abstractions;

namespace GameIntel.Providers.Poe2;

public sealed class Poe2Provider(IPoeNinjaClient client) : IGameProvider
{
    public const string ProviderId = "poe2";

    public string Id => ProviderId;

    public string DisplayName => "Path of Exile 2";

    public string DataSource => "poe.ninja economy API (https://poe.ninja)";

    public IReadOnlyList<string> Capabilities { get; } =
    [
        "Price check any exchange item (currency, runes, essences, soul cores, fragments, ...)",
        "Most valuable items in a category for the current or a named league",
        "Biggest market movers in a category",
    ];

    public async Task<ProviderHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var leagues = await client.GetLeaguesAsync(cancellationToken).ConfigureAwait(false);
            return leagues.Count > 0
                ? new ProviderHealth(true, $"Current league: {leagues[0].Name}")
                : ProviderHealth.Unhealthy("No leagues returned.");
        }
        catch (GameIntelException ex)
        {
            return ProviderHealth.Unhealthy(ex.Message);
        }
    }
}
