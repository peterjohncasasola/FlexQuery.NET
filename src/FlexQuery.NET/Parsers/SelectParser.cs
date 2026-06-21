using FlexQuery.NET.Models;
using System.Text.RegularExpressions;
using FlexQuery.NET.Constants;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Parses SELECT expressions including aggregate functions.
/// Format: field1,field2,sum(total),count(id)
/// </summary>
internal static class SelectParser
{
    private static readonly Regex SelectAggregatePattern = new(
        @"^(?:(?<fn>sum|count|avg|average|min|max)\((?<field>[A-Za-z_][A-Za-z0-9_\.]*)?\)|(?<field2>[A-Za-z_][A-Za-z0-9_\.]*)\.(?<fn2>sum|count|avg|average|min|max)\(\))$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses a select string and populates the options with scalars and aggregates.
    /// Scalar fields go to options.Select, aggregate functions go to options.Aggregates.
    /// </summary>
    public static void Parse(QueryOptions options, string? rawSelect)
    {
        var fields = ParserUtilities.SplitCsv(rawSelect);
        if (fields.Count == 0)
        {
            options.Select = [];
            return;
        }

        var scalars = new List<string>();

        foreach (var field in fields)
        {
            var match = SelectAggregatePattern.Match(field);
            if (!match.Success)
            {
                scalars.Add(field);
                continue;
            }

            var fn = match.Groups[QueryOptionKeys.Fn].Success
                ? match.Groups[QueryOptionKeys.Fn].Value.ToLowerInvariant()
                : match.Groups["fn2"].Value.ToLowerInvariant();

            if (fn == "average") fn = "avg";

            var aggregateField = match.Groups[QueryOptionKeys.Field].Success
                ? match.Groups[QueryOptionKeys.Field].Value
                : (match.Groups[QueryOptionKeys.Field2].Success ? match.Groups[QueryOptionKeys.Field2].Value : null);

            options.Aggregates.Add(new AggregateModel
            {
                Function = fn,
                Field = string.IsNullOrWhiteSpace(aggregateField) ? null : aggregateField,
                Alias = ParserUtilities.BuildAggregateAlias(fn, aggregateField)
            });
        }

        options.Select = scalars;
    }
}