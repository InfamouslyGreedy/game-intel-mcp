using GameIntel.Core.Abstractions;
using GameIntel.McpServer;

// Two transports, one set of tools:
//   default  → stdio, for local clients like Claude Desktop, Claude Code or VS Code.
//   --http   → Streamable HTTP on /mcp, for container hosting (Docker, Azure Container Apps).
if (args.Contains("--http", StringComparer.OrdinalIgnoreCase))
{
    await RunHttpAsync(args).ConfigureAwait(false);
}
else
{
    await RunStdioAsync(args).ConfigureAwait(false);
}

static async Task RunStdioAsync(string[] args)
{
    // MCP clients launch the server from their own working directory, so anchor config to the binary.
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory,
    });

    // stdout carries the MCP protocol; any log line written there corrupts it. Send all logs to stderr.
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

    builder.Services.AddGameIntel(builder.Configuration);
    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
        .AddGameIntelTools();

    await builder.Build().RunAsync().ConfigureAwait(false);
}

static async Task RunHttpAsync(string[] args)
{
    var builder = WebApplication.CreateBuilder(args.Where(a => !a.Equals("--http", StringComparison.OrdinalIgnoreCase)).ToArray());

    builder.Services.AddGameIntel(builder.Configuration);
    builder.Services.AddMcpServer()
        .WithHttpTransport(o => o.Stateless = true)
        .AddGameIntelTools();

    var app = builder.Build();

    // Optional shared-secret gate. Fine for a personal deployment; for anything multi-user,
    // put the server behind Entra ID / API Management and use OAuth (see README roadmap).
    var apiKey = app.Configuration["Mcp:ApiKey"];
    if (!string.IsNullOrEmpty(apiKey))
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/mcp")
                && !string.Equals(context.Request.Headers["X-Api-Key"], apiKey, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next(context).ConfigureAwait(false);
        });
    }

    app.MapGet("/healthz", async (GameProviderRegistry registry, CancellationToken ct) =>
    {
        var checks = await Task.WhenAll(registry.All.Select(async p => new
        {
            game = p.Id,
            health = await p.CheckHealthAsync(ct).ConfigureAwait(false),
        })).ConfigureAwait(false);

        // The server itself is healthy even if an upstream game API is down; report per-provider status.
        return Results.Ok(checks.Select(c => new { c.game, healthy = c.health.IsHealthy, detail = c.health.Detail }));
    });

    app.MapMcp("/mcp");

    await app.RunAsync().ConfigureAwait(false);
}
