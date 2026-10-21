using System.ComponentModel;
using GameIntel.Core.Abstractions;
using ModelContextProtocol.Server;

namespace GameIntel.McpServer.Tools;

/// <summary>Game-agnostic tools. These work unchanged no matter which providers are registered.</summary>
[McpServerToolType]
public sealed class GameTools(GameProviderRegistry registry, ToolRunner runner)
{
    [McpServerTool(Name = "list_games", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description("Lists the games this server supports, what each can answer, where its data comes from, and whether its upstream API is currently reachable. Call this first if unsure which tool to use.")]
    public Task<string> ListGames(CancellationToken cancellationToken) =>
        runner.RunAsync("list_games", async () =>
        {
            var games = new List<object>();
            foreach (var provider in registry.All.OrderBy(p => p.Id, StringComparer.Ordinal))
            {
                var health = await provider.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
                games.Add(new
                {
                    provider.Id,
                    provider.DisplayName,
                    provider.DataSource,
                    provider.Capabilities,
                    Status = health.IsHealthy ? "available" : "unavailable",
                    health.Detail,
                });
            }

            return games;
        });
}
