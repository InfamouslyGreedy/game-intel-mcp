using System.Threading.RateLimiting;

namespace GameIntel.Core.Http;

/// <summary>
/// A process-wide token bucket for one upstream. Registered as a singleton per client type so
/// that every (transient) typed HttpClient for that upstream shares the same budget.
/// </summary>
/// <typeparam name="TClient">The API client this limiter belongs to; only used to key DI.</typeparam>
public sealed class ProviderRateLimiter<TClient> : IDisposable
{
    public ProviderRateLimiter(ApiClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var perMinute = Math.Max(1, options.PermitsPerMinute);
        var burst = Math.Clamp(options.BurstSize, 1, perMinute);

        // Refill in small steps so the budget is spread across the minute instead of one big refill.
        var replenishPeriod = TimeSpan.FromSeconds(60.0 / perMinute);

        Limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = burst,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = replenishPeriod,
            QueueLimit = Math.Max(0, options.QueueLimit),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    }

    public RateLimiter Limiter { get; }

    public void Dispose() => Limiter.Dispose();
}
