using System.Text.RegularExpressions;
using DynamicQueryable.Models;
using DynamicQueryable.Parsers.Dsl;
using DynamicQueryable.Parsers.Jql;

namespace DynamicQueryable.Parsers;

/// <summary>
/// Parses the <c>include</c> query-string parameter into a list of
/// <see cref="IncludeNode"/> trees.
///
/// <para><b>Supported syntax</b></para>
/// <code>
/// include=orders
/// include=orders,profile
/// include=orders(status = Cancelled).orderItems(id = 101)
/// include=orders(status = Cancelled AND total > 500).orderItems
/// </code>
///
/// <para>
/// Dot (<c>.</c>) chains levels. Parentheses after a segment introduce an
/// inline JQL filter for that level. Multiple top-level include chains are
/// comma-separated.
/// </para>
/// </summary>
public static class FilteredIncludeParser
{
    // Pre-compiled: matches  name  or  name(...)  at the start of the string.
    // Group 1 = property name, Group 2 = filter content (without parens) or null.
    private static readonly Regex SegmentRegex = new(
        @"^(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:\((?<filter>.*)\))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    // ── Public entry point ───────────────────────────────────────────────

    /// <summary>
    /// Parses a raw <c>include</c> query-string value into a list of
    /// <see cref="IncludeNode"/> trees.
    /// Returns an empty list for null / whitespace input.
    /// </summary>
    public static List<IncludeNode> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];

        // Split top-level chains by comma but NOT commas inside parentheses.
        var chains = SplitTopLevel(raw, ',');
        var result = new List<IncludeNode>(chains.Count);
        foreach (var chain in chains)
        {
            var node = ParseChain(chain.Trim());
            if (node is not null) result.Add(node);
        }
        return result;
    }

    // ── Chain parsing ────────────────────────────────────────────────────

    /// <summary>
    /// Parses a single dot-separated chain (e.g.
    /// <c>orders(status = Cancelled).orderItems(id = 101)</c>) into a
    /// linked <see cref="IncludeNode"/> tree.
    /// </summary>
    private static IncludeNode? ParseChain(string chain)
    {
        if (string.IsNullOrWhiteSpace(chain)) return null;

        // Split on dots that are NOT inside parentheses.
        var segments = SplitTopLevel(chain, '.');

        IncludeNode? root = null;
        IncludeNode? current = null;

        foreach (var segment in segments)
        {
            var node = ParseSegment(segment.Trim());
            if (node is null) break;

            if (root is null)
            {
                root = current = node;
            }
            else
            {
                current!.Children.Add(node);
                current = node;
            }
        }

        return root;
    }

    /// <summary>
    /// Parses a single segment: <c>propertyName</c> or
    /// <c>propertyName(jqlFilter)</c>.
    /// </summary>
    private static IncludeNode? ParseSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment)) return null;

        var match = SegmentRegex.Match(segment);
        if (!match.Success) return null;

        var name = match.Groups["name"].Value;
        FilterGroup? filter = null;

        if (match.Groups["filter"].Success)
        {
            var rawFilter = match.Groups["filter"].Value.Trim();
            filter = TryParseFilter(rawFilter);
        }

        return new IncludeNode { Path = name, Filter = filter };
    }

    // ── Filter parsing (delegates to JQL) ────────────────────────────────

    private static FilterGroup? TryParseFilter(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // Auto-detect format: if it contains a colon, try DSL first.
        if (raw.Contains(':'))
        {
            try
            {
                var dslAst = DslParser.Parse(raw);
                return DslFilterConverter.ToFilterGroup(dslAst);
            }
            catch { /* fallback to JQL */ }
        }

        try
        {
            var jqlAst = JqlParser.Parse(raw);
            return JqlFilterConverter.ToFilterGroup(jqlAst);
        }
        catch
        {
            return null;
        }
    }

    // ── Utility: split on delimiter ignoring depth ────────────────────────

    /// <summary>
    /// Splits <paramref name="input"/> on <paramref name="delimiter"/> while
    /// ignoring occurrences inside balanced parentheses.
    /// </summary>
    private static List<string> SplitTopLevel(string input, char delimiter)
    {
        var parts = new List<string>();
        int depth = 0;
        int start = 0;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == delimiter && depth == 0)
            {
                parts.Add(input[start..i]);
                start = i + 1;
            }
        }

        parts.Add(input[start..]);
        return parts;
    }
}
