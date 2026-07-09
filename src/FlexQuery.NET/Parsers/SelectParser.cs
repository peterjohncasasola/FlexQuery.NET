using FlexQuery.NET.Models;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Parses SELECT expressions into a list of scalar field paths.
/// Aggregate expressions are no longer parsed from select;
/// they are handled by <see cref="AggregateParser"/> instead.
/// </summary>
internal static class SelectParser
{
    /// <summary>
    /// Parses a select string into scalar field paths on options.Select.
    /// </summary>
    public static void Parse(QueryOptions options, string? rawSelect)
    {
        options.Select = ParserUtilities.SplitCsv(rawSelect);
    }
}