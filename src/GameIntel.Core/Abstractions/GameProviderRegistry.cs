namespace GameIntel.Core.Abstractions;

/// <summary>Looks up registered providers by id.</summary>
public sealed class GameProviderRegistry
{
    private readonly Dictionary<string, IGameProvider> _providers;

    public GameProviderRegistry(IEnumerable<IGameProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providers = new Dictionary<string, IGameProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            if (!_providers.TryAdd(provider.Id, provider))
            {
                throw new InvalidOperationException($"Two game providers registered the id '{provider.Id}'.");
            }
        }
    }

    public IReadOnlyCollection<IGameProvider> All => _providers.Values;

    public bool TryGet(string id, out IGameProvider? provider) => _providers.TryGetValue(id, out provider);
}
