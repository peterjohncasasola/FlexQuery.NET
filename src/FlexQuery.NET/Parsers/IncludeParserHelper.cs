using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Parsers;

internal static class IncludeParserHelper
{
    public static List<IncludeNode> Parse(string? raw, Func<string, FilterGroup?> parseFilter)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];

        var chains = SplitTopLevel(raw, ',');
        var result = new List<IncludeNode>(chains.Count);
        foreach (var chain in chains)
        {
            var node = ParseChain(chain.Trim(), parseFilter);
            if (node is not null) result.Add(node);
        }
        return result;
    }

    private static IncludeNode? ParseChain(string chain, Func<string, FilterGroup?> parseFilter)
    {
        if (string.IsNullOrWhiteSpace(chain)) return null;

        var segments = SplitTopLevel(chain, '.');
        IncludeNode? root = null;
        IncludeNode? current = null;

        foreach (var segment in segments)
        {
            var node = ParseSegment(segment.Trim(), parseFilter);
            if (node is null) break;

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

    private static IncludeNode? ParseSegment(string segment, Func<string, FilterGroup?> parseFilter)
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
