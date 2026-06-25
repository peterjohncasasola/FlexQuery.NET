using FlexQuery.NET.Models;
using FlexQuery.NET.Constants;
using System.Text.RegularExpressions;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Parses HAVING clause expressions for grouped queries.
/// Format: sum(field):gt:value or count():eq:value
/// </summary>
internal static class HavingParser
{
    private static readonly Regex HavingPattern = new(
        @"^(?<fn>sum|count|avg|average|min|max)(?:\((?<field>[A-Za-z_][A-Za-z0-9_\.]*)?\)|:(?<field2>[A-Za-z_][A-Za-z0-9_\.]+))?:(?<op>[A-Za-z_][A-Za-z0-9_]*):(?<value>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses a HAVING clause string into a <see cref="HavingCondition"/>.
    /// Returns null if the input is null, empty, or malformed.
    /// </summary>
    public static HavingCondition? Parse(string? rawHaving)
    {
        if (string.IsNullOrWhiteSpace(rawHaving)) return null;
        
        var match = HavingPattern.Match(rawHaving.Trim());
        if (!match.Success) return null;

        var fn = match.Groups[QueryOptionKeys.Fn].Value.ToLowerInvariant();
        if (fn == "average") fn = "avg";
        var field = match.Groups[QueryOptionKeys.Field].Success 
            ? match.Groups[QueryOptionKeys.Field].Value 
            : (match.Groups[QueryOptionKeys.Field2].Success ? match.Groups[QueryOptionKeys.Field2].Value : null);

        return new HavingCondition
        {
            Function = fn,
            Field = string.IsNullOrWhiteSpace(field) ? null : field,
            Operator = FilterOperators.Normalize(match.Groups["op"].Value),
            Value = match.Groups[QueryOptionKeys.Value].Value
        };
    }
}