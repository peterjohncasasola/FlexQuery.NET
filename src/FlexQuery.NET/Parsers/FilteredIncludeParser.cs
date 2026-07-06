using System.Text.RegularExpressions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers.Dsl;

namespace FlexQuery.NET.Parsers;

internal static class JqlSyntaxDetector
{
    private static readonly string[] JqlPatterns =
    [
        @"\s*=\s*[^:=\s]",       // =  (but not := or ==)
        @"\s*!=\s*",              // !=
        @"\s*>\s*",               // >
        @"\s*>=\s*",              // >=
        @"\s*<\s*",               // <
        @"\s*<=\s*",              // <=
    ];

    private static readonly string[] JqlKeywords =
    [
        " AND ", " OR ", " NOT ", " IN ",
    ];

    public static bool IsJql(string raw)
    {
        var upper = raw.ToUpperInvariant();
        foreach (var kw in JqlKeywords)
        {
            if (upper.Contains(kw, StringComparison.Ordinal)) return true;
        }

        foreach (var pat in JqlPatterns)
        {
            if (Regex.IsMatch(raw, pat, RegexOptions.CultureInvariant)) return true;
        }

        return false;
    }
}

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
internal static class FilteredIncludeParser
{

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

        var span = segment.AsSpan();
        var openParen = span.IndexOf('(');
        
        ReadOnlySpan<char> nameSpan;
        ReadOnlySpan<char> filterSpan = default;

        if (openParen < 0)
        {
            nameSpan = span.Trim();
        }
        else
        {
            var closeParen = span.LastIndexOf(')');
            if (closeParen < openParen) return null;
            
            nameSpan = span[..openParen].Trim();
            filterSpan = span[(openParen + 1)..closeParen].Trim();
        }

        if (nameSpan.IsEmpty) return null;

        var name = nameSpan.ToString();
        FilterGroup? filter = filterSpan.IsEmpty ? null : TryParseFilter(filterSpan.ToString());

        return new IncludeNode { Path = name, Filter = filter };
    }

    // ── Filter parsing (DSL only) ────────────────────────────────────────

    private static FilterGroup? TryParseFilter(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        if (!raw.Contains(':'))
        {
            if (JqlSyntaxDetector.IsJql(raw))
            {
                throw new InvalidOperationException(
                    "JQL syntax is no longer supported in filtered includes. " +
                    "Use FlexQuery DSL syntax instead. Example: orders(Status:eq:Cancelled)");
            }

            return null;
        }

        try
        {
            var dslAst = Dsl.DslAstParser.Parse(raw);
            return DslFilterConverter.ToFilterGroup(dslAst);
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
