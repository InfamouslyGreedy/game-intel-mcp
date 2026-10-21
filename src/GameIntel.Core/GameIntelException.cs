namespace GameIntel.Core;

/// <summary>
/// Base type for errors whose message is safe and useful to show to the calling agent
/// (bad input, unknown hero, upstream outage). The MCP layer turns these into tool errors;
/// anything else is treated as an internal failure and its details are not leaked.
/// </summary>
public class GameIntelException : Exception
{
    public GameIntelException() { }

    public GameIntelException(string message) : base(message) { }

    public GameIntelException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>The caller asked for something that doesn't exist (unknown hero, item, league, player).</summary>
public sealed class NotFoundException : GameIntelException
{
    public NotFoundException() { }

    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string message, IReadOnlyList<string> suggestions)
        : base(suggestions.Count == 0 ? message : $"{message} Did you mean: {string.Join(", ", suggestions)}?")
    {
        Suggestions = suggestions;
    }

    public NotFoundException(string message, Exception innerException) : base(message, innerException) { }

    public IReadOnlyList<string> Suggestions { get; } = [];
}

/// <summary>The upstream API failed after retries and no cached copy was available.</summary>
public sealed class UpstreamUnavailableException : GameIntelException
{
    public UpstreamUnavailableException() { }

    public UpstreamUnavailableException(string message) : base(message) { }

    public UpstreamUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}
