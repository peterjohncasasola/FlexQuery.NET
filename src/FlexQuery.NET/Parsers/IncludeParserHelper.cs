using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Parsers;

internal static class IncludeParserHelper
{
    public static List<IncludeNode> Parse(string? raw,
        Func<string, FilterGroup?> parseFilter,
        Func<string, Exception> createException)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];

        var chains = SplitTopLevel(raw, ',');
        var result = new List<IncludeNode>(chains.Count);
        foreach (var chain in chains)
        {
            var trimmed = chain.Trim();
            if (trimmed.Length == 0)
                throw createException($"Include expression contains an empty segment. Expected format: NavigationPath[(filter)].");

            var node = ParseChain(trimmed, parseFilter, createException);
            if (node is not null) result.Add(node);
        }
        return result;
    }

    private static IncludeNode? ParseChain(string chain,
        Func<string, FilterGroup?> parseFilter,
        Func<string, Exception> createException)
    {
        if (string.IsNullOrWhiteSpace(chain)) return null;

        var segments = SplitTopLevel(chain, '.');
        IncludeNode? root = null;
        IncludeNode? current = null;

        foreach (var segment in segments)
        {
            var trimmed = segment.Trim();
            var node = ParseSegment(trimmed, parseFilter, createException);
            if (node is null)
                throw createException(
                    $"Invalid include segment '{trimmed}' in chain '{chain}'. " +
                    "Expected format: NavigationPath[(filter)].");

            if (root is null)
                root = current = node;
            else
            {
                current!.Children.Add(node);
                current = node;
            }
        }

        return root;
    }

    private static IncludeNode? ParseSegment(string segment,
        Func<string, FilterGroup?> parseFilter,
        Func<string, Exception> createException)
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
            if (closeParen < openParen)
                throw createException(
                    $"Include segment '{segment}' has an opening parenthesis without a matching closing parenthesis.");

            nameSpan = span[..openParen].Trim();
            filterSpan = span[(openParen + 1)..closeParen].Trim();
        }

        if (nameSpan.IsEmpty)
            throw createException(
                $"Include segment has an empty navigation property name.");

        var name = nameSpan.ToString();
        if (!ParserUtilities.IsValidPropertyPath(name.AsSpan()))
            throw createException(
                $"Invalid navigation property path '{name}' in include expression. " +
                "Property paths must be dot-separated identifiers (e.g. 'Orders' or 'Orders.Items').");

        var filter = filterSpan.IsEmpty ? null : parseFilter(filterSpan.ToString());

        return new IncludeNode { Path = name, Filter = filter };
    }

    public static List<string> SplitTopLevel(string input, char delimiter)
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
