using System.Globalization;
using System.Text;

namespace GameIntel.Core.Text;

/// <summary>
/// Forgiving name lookup for things players type from memory: "AM" vs "Anti-Mage",
/// "divine orb" vs "Divine Orb", "exalt" vs "Exalted Orb". Agents pass through whatever
/// the user said, so tools must not demand exact spellings.
/// </summary>
public static class NameMatcher
{
    /// <summary>
    /// Finds the best candidate for <paramref name="query"/>. Ranking: exact (normalized) match,
    /// then alias match, then prefix, then substring, then word-initials ("am" → "Anti-Mage").
    /// Returns null when nothing matches; use <see cref="Suggest{T}"/> to offer alternatives.
    /// </summary>
    public static T? FindBest<T>(
        IEnumerable<T> candidates,
        string query,
        Func<T, string> name,
        Func<T, IEnumerable<string>>? aliases = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(name);

        var q = Normalize(query);
        if (q.Length == 0)
        {
            return null;
        }

        T? prefix = null, contains = null, initials = null;

        foreach (var candidate in candidates)
        {
            var n = Normalize(name(candidate));
            if (n == q)
            {
                return candidate;
            }

            if (aliases is not null && aliases(candidate).Any(a => Normalize(a) == q))
            {
                return candidate;
            }

            if (prefix is null && n.StartsWith(q, StringComparison.Ordinal))
            {
                prefix = candidate;
            }
            else if (contains is null && n.Contains(q, StringComparison.Ordinal))
            {
                contains = candidate;
            }
            else if (initials is null && q.Length >= 2 && Initials(name(candidate)) == q)
            {
                initials = candidate;
            }
        }

        return prefix ?? contains ?? initials;
    }

    /// <summary>Closest names by edit distance, for "did you mean" messages.</summary>
    public static IReadOnlyList<string> Suggest<T>(IEnumerable<T> candidates, string query, Func<T, string> name, int max = 3)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(name);

        var q = Normalize(query);
        return candidates
            .Select(c => name(c))
            .Select(n => (Name: n, Distance: Levenshtein(Normalize(n), q)))
            .Where(x => x.Distance <= Math.Max(2, q.Length / 2))
            .OrderBy(x => x.Distance)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .Select(x => x.Name)
            .ToList();
    }

    /// <summary>Lowercase, letters and digits only: "Anti-Mage" → "antimage".</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Normalize(NormalizationForm.FormD))
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
        }

        return sb.ToString();
    }

    private static string Initials(string value)
    {
        var sb = new StringBuilder();
        var atWordStart = true;
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                if (atWordStart)
                {
                    sb.Append(char.ToLower(ch, CultureInfo.InvariantCulture));
                }

                atWordStart = false;
            }
            else
            {
                atWordStart = true;
            }
        }

        return sb.ToString();
    }

    internal static int Levenshtein(string a, string b)
    {
        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
