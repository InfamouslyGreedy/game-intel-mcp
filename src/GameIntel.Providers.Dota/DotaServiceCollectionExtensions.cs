using GameIntel.Core.Abstractions;
using GameIntel.Core.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace GameIntel.Providers.Dota;

public static class DotaServiceCollectionExtensions
{
    private static readonly Uri DefaultBaseAddress = new("https://api.opendota.com/api/");

    /// <summary>
    /// Registers the Dota 2 provider. Settings bind from <paramref name="section"/>
    /// (e.g. "Providers:Dota2"); every value has a sensible default, so the section is optional.
    /// </summary>
    public static IServiceCollection AddDotaProvider(this IServiceCollection services, IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(section);

        services.AddOptions<ApiClientOptions>(DotaProvider.ProviderId)
            .Configure(o =>
            {
                // OpenDota free tier: 60 requests/minute, 2,000/day without a key.
                o.BaseAddress = DefaultBaseAddress;
                o.PermitsPerMinute = 60;
                o.BurstSize = 10;
            })
            .Bind(section);

        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();

        services.TryAddSingleton(sp => new ProviderRateLimiter<OpenDotaClient>(
            sp.GetRequiredService<IOptionsMonitor<ApiClientOptions>>().Get(DotaProvider.ProviderId)));

        services.AddHttpClient<IOpenDotaClient, OpenDotaClient>((sp, http) =>
        {
            var o = sp.GetRequiredService<IOptionsMonitor<ApiClientOptions>>().Get(DotaProvider.ProviderId);
            http.BaseAddress = o.BaseAddress ?? DefaultBaseAddress;
            http.Timeout = o.Timeout;
            http.DefaultRequestHeaders.UserAgent.ParseAdd(o.UserAgent);
        });

        services.TryAddTransient<DotaService>();
        // Transient (not singleton) so it never captures a typed HttpClient past its handler lifetime.
        services.AddTransient<IGameProvider, DotaProvider>();
        return services;
    }
}
