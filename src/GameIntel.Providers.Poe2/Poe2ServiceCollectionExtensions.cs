using GameIntel.Core.Abstractions;
using GameIntel.Core.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace GameIntel.Providers.Poe2;

public static class Poe2ServiceCollectionExtensions
{
    private static readonly Uri DefaultBaseAddress = new("https://poe.ninja/");

    /// <summary>Registers the PoE 2 provider. Settings bind from <paramref name="section"/> (e.g. "Providers:Poe2").</summary>
    public static IServiceCollection AddPoe2Provider(this IServiceCollection services, IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(section);

        services.AddOptions<ApiClientOptions>(Poe2Provider.ProviderId)
            .Configure(o =>
            {
                // poe.ninja publishes no numeric limit but blocks heavy clients. With a 15-minute
                // cache, a handful of requests per minute is plenty.
                o.BaseAddress = DefaultBaseAddress;
                o.PermitsPerMinute = 12;
                o.BurstSize = 4;
            })
            .Bind(section);

        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();

        services.TryAddSingleton(sp => new ProviderRateLimiter<PoeNinjaClient>(
            sp.GetRequiredService<IOptionsMonitor<ApiClientOptions>>().Get(Poe2Provider.ProviderId)));

        services.AddHttpClient<IPoeNinjaClient, PoeNinjaClient>((sp, http) =>
        {
            var o = sp.GetRequiredService<IOptionsMonitor<ApiClientOptions>>().Get(Poe2Provider.ProviderId);
            http.BaseAddress = o.BaseAddress ?? DefaultBaseAddress;
            http.Timeout = o.Timeout;
            http.DefaultRequestHeaders.UserAgent.ParseAdd(o.UserAgent);
        });

        services.TryAddTransient<Poe2Service>();
        services.AddTransient<IGameProvider, Poe2Provider>();
        return services;
    }
}
