namespace GameIntel.Core.Http;

/// <summary>Per-upstream settings, bound from configuration (e.g. "Providers:Dota2:Api").</summary>
public sealed class ApiClientOptions
{
    public Uri? BaseAddress { get; set; }

    /// <summary>Sent on every request. poe.ninja asks clients to identify themselves with a contact.</summary>
    public string UserAgent { get; set; } = "GameIntelMcp/0.1 (+https://github.com/InfamouslyGreedy/game-intel-mcp)";

    /// <summary>Optional upstream API key (OpenDota supports one for higher limits).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Token-bucket budget. Requests queue (rather than fail) when the bucket is empty.</summary>
    public int PermitsPerMinute { get; set; } = 60;

    public int BurstSize { get; set; } = 10;

    public int QueueLimit { get; set; } = 50;

    /// <summary>Retries for 429, 5xx and transient network errors.</summary>
    public int MaxRetries { get; set; } = 3;

    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// How long a cached response may be served after it goes stale, either while revalidating
    /// with an ETag or as a fallback when the upstream is down.
    /// </summary>
    public TimeSpan StaleRetention { get; set; } = TimeSpan.FromHours(6);
}
