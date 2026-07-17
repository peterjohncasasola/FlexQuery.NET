using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Parsers.Dsl;
using System.Text.RegularExpressions;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Parses HAVING clause expressions for grouped queries.
/// Format: sum(field):gt:value or count():eq:value
/// </summary>
internal static class DslHavingParser
{
    private static readonly Regex HavingPattern = new(
        @"^(?<fn>sum|count|avg|average|min|max)(?:\((?<field>[A-Za-z_][A-Za-z0-9_\.]*)?\)|:(?<field2>[A-Za-z_][A-Za-z0-9_\.]+))?:(?<op>[A-Za-z_][A-Za-z0-9_]*):(?<value>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses a HAVING clause string into a <see cref="HavingCondition"/>.
    /// Throws <see cref="DslParseException"/> if the input is malformed.
    /// </summary>
    public static HavingCondition? Parse(string? rawHaving)
    {
        if (string.IsNullOrWhiteSpace(rawHaving)) return null;
        
        var trimmed = rawHaving.Trim();
        var match = HavingPattern.Match(trimmed);
        if (!match.Success)
            throw new DslParseException(
                $"Unable to parse HAVING expression '{rawHaving}'. " +
                "Expected format: FUNCTION:Field:OPERATOR:value. " +
                "For example: sum:total:gt:100");

        var fnRaw = match.Groups[QueryOptionKeys.Fn].Value.ToLowerInvariant();
        if (fnRaw == "average") fnRaw = "avg";
        var field = match.Groups[QueryOptionKeys.Field].Success 
            ? match.Groups[QueryOptionKeys.Field].Value 
            : (match.Groups[QueryOptionKeys.Field2].Success ? match.Groups[QueryOptionKeys.Field2].Value : null);

        if (field is not null && !ParserUtilities.IsValidPropertyPath(field.AsSpan()))
            throw new DslParseException(
                $"Invalid field '{field}' in HAVING expression '{rawHaving}'. " +
                "Field must be a valid property path.");

        var function = AggregateFunctionConverter.Parse(fnRaw);
        if (function == AggregateFunction.Count && string.IsNullOrEmpty(field))
        {
            throw new DslParseException(
                $"Unable to parse HAVING expression '{rawHaving}'. " +
                $"COUNT(*) is not supported. Use COUNT(<collection>) or another aggregate over a property instead.");
        }

        return new HavingCondition
        {
            Function = function,
            Field = string.IsNullOrWhiteSpace(field) ? null : field,
            Operator = FilterOperators.Normalize(match.Groups["op"].Value),
            Value = match.Groups[QueryOptionKeys.Value].Value
        };
    }
}