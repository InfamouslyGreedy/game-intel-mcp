# Adding a game

This walks through adding a new provider, using a hypothetical "Deadlock" provider as the example. Nothing in the existing providers changes.

## 1. Create the project

```bash
dotnet new classlib -o src/GameIntel.Providers.Deadlock
dotnet sln GameIntel.slnx add src/GameIntel.Providers.Deadlock
```

Copy the `<ItemGroup>`s from `GameIntel.Providers.Dota.csproj`: the `Microsoft.AspNetCore.App` framework reference, the project reference to `GameIntel.Core`, and `InternalsVisibleTo` for the tests.

## 2. Write a typed client on top of `ResilientApiClient`

You get caching, rate limiting, ETags, retries and stale-if-error for free. Your client only decides URLs and how long each dataset stays fresh.

```csharp
public sealed class DeadlockClient : IDeadlockClient
{
    private readonly ResilientApiClient _api;

    public DeadlockClient(HttpClient http, IMemoryCache cache, ProviderRateLimiter<DeadlockClient> limiter,
        IOptionsMonitor<ApiClientOptions> options, TimeProvider time, ILogger<DeadlockClient> logger)
    {
        _api = new ResilientApiClient("deadlock", http, cache, limiter.Limiter,
            options.Get(DeadlockProvider.ProviderId), new JsonSerializerOptions(JsonSerializerDefaults.Web), time, logger);
    }

    public Task<IReadOnlyList<Hero>> GetHeroesAsync(CancellationToken ct) =>
        _api.GetJsonAsync<IReadOnlyList<Hero>>("heroes", TimeSpan.FromHours(24), ct);
}
```

## 3. Keep logic pure

Put calculations in a static `DeadlockAnalyzer` that takes data in and returns report records. Unit-test it with small hand-written inputs. Services fetch and delegate; analyzers decide.

## 4. Implement `IGameProvider`

Give it a lowercase `Id` (used as the tool-name prefix), a display name, a data-source string and a capability list, plus a cheap `CheckHealthAsync`.

## 5. Register it

Add `DeadlockServiceCollectionExtensions.AddDeadlockProvider(IConfigurationSection)` by copying the Dota one (options defaults, rate limiter, typed `HttpClient`, service, provider). Then add one line to `ServerSetup.AddGameIntel`:

```csharp
services.AddDeadlockProvider(configuration.GetSection("Providers:Deadlock"));
```

## 6. Expose tools

Create `src/GameIntel.McpServer/Tools/DeadlockTools.cs` with `[McpServerToolType]`, name each tool `deadlock_<verb>`, and register it in `AddGameIntelTools()`:

```csharp
.WithTools<DeadlockTools>()
```

Write tool and parameter descriptions for the model: say what the tool is good for, what the inputs accept, and what the output does *not* mean.

`list_games` and `/healthz` pick up the new provider automatically.

## Checklist before opening a PR

- [ ] Respect the upstream's published rate limits and terms; set sensible `PermitsPerMinute` defaults
- [ ] Only use documented or explicitly supported endpoints
- [ ] Fixtures for the responses you parse, and tests for the analyzer
- [ ] README tools table and data-source attribution updated
