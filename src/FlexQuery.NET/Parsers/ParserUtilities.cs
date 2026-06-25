using FlexQuery.NET.Models;

namespace FlexQuery.NET.Parsers;

internal static class ParserUtilities
{
    public static List<string> SplitCsv(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];

        var span = raw.AsSpan();
        var result = new List<string>();

        while (!span.IsEmpty)
        {
            var comma = span.IndexOf(',');
            var part = comma < 0 ? span : span[..comma];
            part = part.Trim();

            if (!part.IsEmpty)
                result.Add(part.ToString());

            if (comma < 0) break;
            span = span[(comma + 1)..];
        }

        return result;
    }
    
    public static LogicOperator ParseLogic(string? raw)
        => string.Equals(raw?.Trim(), "or", StringComparison.OrdinalIgnoreCase)
            ? LogicOperator.Or
            : LogicOperator.And;
    
    public static bool ParseBool(string? raw, bool defaultValue = false)
        => raw is not null 
            ? (raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1")
            : defaultValue;

    public static SortedDictionary<int, Dictionary<string, string>> CollectIndexed(
        IDictionary<string, string> d, string prefix)
    {
        var result = new SortedDictionary<int, Dictionary<string, string>>();
        var prefixSpan = prefix.AsSpan();

        foreach (var kv in d)
        {
            if (TryParseIndexedKey(kv.Key.AsSpan(), prefixSpan, out var idx, out var subkey))
            {
                if (!result.TryGetValue(idx, out var inner))
                    result[idx] = inner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                inner[subkey] = kv.Value;
            }
        }

        return result;
    }
    
    public static ProjectionMode ParseProjectionMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return ProjectionMode.Nested;
        
        return mode.Trim().ToLowerInvariant() switch
        {
            "flat" => ProjectionMode.Flat,
            "flat-mixed" => ProjectionMode.FlatMixed,
            _ => ProjectionMode.Nested
        };
    }
    
    public static int ParseInt(IDictionary<string, string> d, string key, int defaultValue)
        => d.TryGetValue(key, out var raw) && int.TryParse(raw, out var val) ? val : defaultValue;
    
    /// <summary>
    /// Builds a camelCase alias for aggregate functions (e.g., "totalSum" or "Count").
    /// For field-less aggregates (e.g. count()) the alias is just the function name (e.g. "Count").
    /// </summary>
    public static string BuildAggregateAlias(string function, string? field)
    {
        var fn = function.ToLowerInvariant();
        var functionName = $"{char.ToUpperInvariant(fn[0])}{fn[1..]}";

        if (string.IsNullOrWhiteSpace(field))
            return functionName;

        var normalized = string.Join("", field.Replace('.', '_')
            .Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select((part, i) => i == 0
                ? char.ToLowerInvariant(part[0]) + part[1..]
                : char.ToUpperInvariant(part[0]) + part[1..]));

        return $"{normalized}{functionName}";
    }

    private static bool TryParseIndexedKey(
        ReadOnlySpan<char> key,
        ReadOnlySpan<char> prefix,
        out int index,
        out string subKey)
    {
        index = 0;
        subKey = string.Empty;

        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var pos = prefix.Length;
        if (pos >= key.Length || key[pos++] != '[') return false;

        var start = pos;
        while (pos < key.Length && char.IsDigit(key[pos])) pos++;
        if (start == pos || pos >= key.Length || key[pos++] != ']') return false;
        
#if NET6_0_OR_GREATER
        if (!int.TryParse(key[start..(pos - 1)], out index)) return false;
#else
        if (!int.TryParse(key[start..(pos - 1)].ToString(), out index)) return false;
#endif

        if (pos >= key.Length || (key[pos] != '.' && key[pos] != '[')) return false;
        pos++;

        var subStart = pos;
        while (pos < key.Length && key[pos] != ']' && !char.IsWhiteSpace(key[pos])) pos++;
        if (subStart == pos) return false;

        subKey = key[subStart..pos].ToString().ToLowerInvariant();
        return true;
    }
}