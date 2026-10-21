using System.Text.Json;
using System.Text.Json.Serialization;
using GameIntel.Core;
using ModelContextProtocol;

namespace GameIntel.McpServer.Tools;

/// <summary>
/// The one place where domain results and errors become MCP tool output.
/// Results are serialized as indented camelCase JSON (easy for both models and humans to read).
/// Expected failures (<see cref="GameIntelException"/>) become tool errors with their message, so the
/// agent can recover ("did you mean Anti-Mage?"). Anything unexpected is logged and reported
/// generically, so stack traces and upstream details never leak to the client.
/// </summary>
public sealed partial class ToolRunner(ILogger<ToolRunner> logger)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<string> RunAsync<T>(string tool, Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            var result = await action().ConfigureAwait(false);
            return JsonSerializer.Serialize(result, Json);
        }
        catch (GameIntelException ex)
        {
            LogExpectedFailure(tool, ex.Message);
            throw new McpException(ex.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Deliberate catch-all at the tool boundary.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogUnexpectedFailure(ex, tool);
            throw new McpException($"'{tool}' failed unexpectedly. Check the server logs for details.");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Tool {Tool} returned a handled error: {Reason}")]
    private partial void LogExpectedFailure(string tool, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Tool {Tool} failed unexpectedly")]
    private partial void LogUnexpectedFailure(Exception ex, string tool);
}
