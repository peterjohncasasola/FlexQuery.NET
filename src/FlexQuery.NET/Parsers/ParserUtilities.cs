using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Projection;

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
    /// Validates the grammar of a property path (e.g. "Customer.Name").
    /// Returns true if the path is a valid sequence of dot-separated identifiers.
    /// Does NOT perform semantic validation (property existence).
    /// Supports only simple dot-separated paths; indexers, wildcards, and bracket access are out of scope.
    /// </summary>
    internal static bool IsValidPropertyPath(ReadOnlySpan<char> path)
    {
        if (path.IsEmpty) return false;
        if (path[0] == '.') return false;
        if (path[^1] == '.') return false;

        int segmentStart = 0;
        for (int i = 0; i <= path.Length; i++)
        {
            if (i == path.Length || path[i] == '.')
            {
                if (i == segmentStart) return false;
                for (int j = segmentStart; j < i; j++)
                    if (!IsValidPropertyPathChar(path[j])) return false;
                segmentStart = i + 1;
            }
        }
        return true;
    }

    private static bool IsValidPropertyPathChar(char c)
        => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>
    /// Builds a PascalCase alias for aggregate functions (e.g., "TotalSum" or "Count").
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
            .Select(part => $"{char.ToUpperInvariant(part[0])}{part[1..]}"));

        return $"{normalized}{functionName}";
    }

}