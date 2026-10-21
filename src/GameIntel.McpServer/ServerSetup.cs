using GameIntel.Core.Abstractions;
using GameIntel.McpServer.Tools;
using GameIntel.Providers.Dota;
using GameIntel.Providers.Poe2;

namespace GameIntel.McpServer;

/// <summary>Service registration shared by both transports, so stdio and HTTP expose identical tools.</summary>
internal static class ServerSetup
{
    public static IServiceCollection AddGameIntel(this IServiceCollection services, IConfiguration configuration)
    {
        // Adding a game = one line here + a tools class below. Nothing else changes.
        services.AddDotaProvider(configuration.GetSection("Providers:Dota2"));
        services.AddPoe2Provider(configuration.GetSection("Providers:Poe2"));

        services.AddTransient<GameProviderRegistry>();
        services.AddSingleton<ToolRunner>();
        return services;
    }

    public static IMcpServerBuilder AddGameIntelTools(this IMcpServerBuilder mcp) =>
        mcp.WithTools<GameTools>()
           .WithTools<DotaTools>()
           .WithTools<Poe2Tools>();
}
