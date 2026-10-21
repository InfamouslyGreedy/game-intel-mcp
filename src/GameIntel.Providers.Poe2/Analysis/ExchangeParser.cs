using System.Globalization;
using System.Text.Json;
using GameIntel.Providers.Poe2.Models;

namespace GameIntel.Providers.Poe2.Analysis;

/// <summary>
/// Tolerant reader for poe.ninja's exchange overview. poe.ninja documents field names but not
/// types, and offers no versioning or SLA, so this parser accepts the plausible variants
/// (ids as strings or objects, item metadata as a map or an array, numbers as strings) and skips
/// what it cannot read rather than failing the whole tool call.
/// </summary>
public static class ExchangeParser
{
    public static MarketSnapshot Parse(JsonElement root, string league, string category)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return new MarketSnapshot(league, category, new Currency("unknown", "Unknown"), null, null, []);
        }

        var core = root.TryGetProperty("core", out var c) && c.ValueKind == JsonValueKind.Object ? c : default;

        var names = ReadItemNames(core);
        var primaryId = ReadId(core, "primary") ?? "unknown";
        var secondaryId = ReadId(core, "secondary");
        var rates = ReadRates(core);

        var entries = new List<MarketEntry>();
        if (root.TryGetProperty("lines", out var lines) && lines.ValueKind == JsonValueKind.Array)
        {
            foreach (var line in lines.EnumerateArray())
            {
                var id = ReadString(line, "id");
                if (id is null)
                {
                    continue;
                }

                entries.Add(new MarketEntry(
                    id,
                    NameFor(id, names),
                    ReadNumber(line, "primaryValue"),
                    ReadNumber(line, "volumePrimaryValue"),
                    ReadChange(line),
                    ReadString(line, "maxVolumeCurrency") is { } pair ? NameFor(pair, names) : null));
            }
        }

        return new MarketSnapshot(
            league,
            category,
            new Currency(primaryId, NameFor(primaryId, names)),
            secondaryId is null ? null : new Currency(secondaryId, NameFor(secondaryId, names)),
            secondaryId is not null && rates.TryGetValue(secondaryId, out var rate) ? rate : null,
            entries);
    }

    public static IReadOnlyList<League> ParseLeagues(JsonElement root)
    {
        // Documented as a plain array; tolerate a { "leagues": [...] } wrapper too.
        var array = root.ValueKind == JsonValueKind.Array
            ? root
            : root.ValueKind == JsonValueKind.Object && root.TryGetProperty("leagues", out var inner) ? inner : default;

        if (array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var leagues = new List<League>();
        foreach (var item in array.EnumerateArray())
        {
            var id = ReadString(item, "id") ?? ReadString(item, "name");
            if (id is not null)
            {
                leagues.Add(new League(id, ReadString(item, "name") ?? id));
            }
        }

        return leagues;
    }

    private static Dictionary<string, string> ReadItemNames(JsonElement core)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (core.ValueKind != JsonValueKind.Object || !core.TryGetProperty("items", out var items))
        {
            return names;
        }

        if (items.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in items.EnumerateObject())
            {
                var name = prop.Value.ValueKind == JsonValueKind.Object ? ReadString(prop.Value, "name") : null;
                if (name is not null)
                {
                    names[prop.Name] = name;
                }
            }
        }
        else if (items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (ReadString(item, "id") is { } id && ReadString(item, "name") is { } name)
                {
                    names[id] = name;
                }
            }
        }

        return names;
    }

    private static Dictionary<string, double> ReadRates(JsonElement core)
    {
        var rates = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (core.ValueKind == JsonValueKind.Object
            && core.TryGetProperty("rates", out var r)
            && r.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in r.EnumerateObject())
            {
                if (AsNumber(prop.Value) is { } value)
                {
                    rates[prop.Name] = value;
                }
            }
        }

        return rates;
    }

    /// <summary>
    /// poe.ninja sparklines (in PoE 1, and likely PoE 2) carry a "totalChange" percentage.
    /// Without it we report no trend rather than guess at what the samples mean.
    /// </summary>
    private static double? ReadChange(JsonElement line) =>
        line.TryGetProperty("sparkline", out var s) && s.ValueKind == JsonValueKind.Object
            ? ReadNumber(s, "totalChange")
            : null;

    private static string? ReadId(JsonElement obj, string property)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Object => ReadString(value, "id"),
            _ => null,
        };
    }

    private static string? ReadString(JsonElement obj, string property)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }

    private static double? ReadNumber(JsonElement obj, string property) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(property, out var value) ? AsNumber(value) : null;

    private static double? AsNumber(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.GetDouble(),
        JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
        _ => null,
    };

    /// <summary>Falls back to a readable version of the id: "greater-exalted-orb" → "Greater Exalted Orb".</summary>
    internal static string NameFor(string id, IReadOnlyDictionary<string, string> names)
    {
        if (names.TryGetValue(id, out var name))
        {
            return name;
        }

        var words = id.Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]);
        return string.Join(' ', words);
    }
}
