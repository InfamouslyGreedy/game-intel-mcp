namespace GameIntel.Core.Abstractions;

/// <summary>
/// A pluggable source of game data. Each game (Dota 2, Path of Exile 2, ...) ships as its own
/// provider assembly and registers one implementation of this interface. The MCP host discovers
/// providers through DI, so adding a game never requires touching existing providers.
/// </summary>
public interface IGameProvider
{
    /// <summary>Stable, lowercase identifier used in tool names (e.g. "dota2", "poe2").</summary>
    string Id { get; }

    string DisplayName { get; }

    /// <summary>Where the data comes from, surfaced to the agent so it can attribute answers.</summary>
    string DataSource { get; }

    /// <summary>Human-readable list of what this provider can answer.</summary>
    IReadOnlyList<string> Capabilities { get; }

    /// <summary>Cheap upstream reachability check used by /healthz and the list_games tool.</summary>
    Task<ProviderHealth> CheckHealthAsync(CancellationToken cancellationToken = default);
}

public sealed record ProviderHealth(bool IsHealthy, string? Detail = null)
{
    public static ProviderHealth Healthy { get; } = new(true);

    public static ProviderHealth Unhealthy(string detail) => new(false, detail);
}
