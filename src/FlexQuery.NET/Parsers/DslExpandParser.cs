using FlexQuery.NET.Parsers.Dsl;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Parses DSL expand expressions into <see cref="ExpandAst"/> trees.
/// <para>
/// Grammar: <c>expand = path[.path...]([filter=...; sort=...; take=...; path(...)]), path(...)</c>
/// </para>
/// </summary>
internal static class DslExpandParser
{
    /// <summary>Parses a DSL expand string into a list of ExpandAst roots.</summary>
    public static List<ExpandAst> Parse(string? expandRaw)
    {
        return string.IsNullOrWhiteSpace(expandRaw) ? [] : ParseExpandList(expandRaw);
    }

    private static List<ExpandAst> ParseExpandList(string input)
    {
        var result = new List<ExpandAst>();
        var depth = 0;
        var blockStart = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];
            switch (ch)
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ',' when depth == 0:
                    result.Add(ParseExpandBlock(input[blockStart..i].Trim()));
                    blockStart = i + 1;
                    break;
            }
        }

        var lastBlock = input[blockStart..].Trim();
        if (lastBlock.Length > 0)
            result.Add(ParseExpandBlock(lastBlock));

        return result;
    }

    private static ExpandAst ParseExpandBlock(string block)
    {
        var parenIndex = block.IndexOf('(');
        if (parenIndex < 0)
        {
            return BuildDottedPathNode(block.Trim());
        }

        var pathStr = block[..parenIndex].Trim();
        var pathSegments = pathStr.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var closeParenIndex = FindMatchingCloseParen(block, parenIndex);
        if (closeParenIndex < 0)
            throw new DslParseException($"Unclosed parenthesis in expand block '{block}'.");

        var optionsStr = block[(parenIndex + 1)..closeParenIndex].Trim();
        var result = new ExpandAst
        {
            Path = pathSegments,
            Children = []
        };
        ParseOptionList(optionsStr, result);

        return result;
    }

    private static ExpandAst BuildDottedPathNode(string pathStr)
    {
        var segments = pathStr.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        
        if (segments.Count == 0)
            throw new DslParseException($"Empty expand path.");

        return new ExpandAst
        {
            Path = segments,
            Children = []
        };
    }

    private static void ParseOptionList(string optionsStr, ExpandAst parent)
    {
        if (string.IsNullOrWhiteSpace(optionsStr))
            return;

        var segments = SplitTopLevel(optionsStr, [';', ',']);
        for (var i = 0; i < segments.Count; i++)
        {
            var trimmed = segments[i].Trim();
            if (trimmed.Length == 0)
            {
                if (i < segments.Count - 1)
                    throw new DslParseException("Empty expand option. Expected filter, sort, take, or a nested expand block.");
                continue;
            }

            var topLevelEq = FindTopLevelEquals(trimmed);
            if (topLevelEq >= 0)
            {
                var key = trimmed[..topLevelEq].Trim();
                var value = trimmed[(topLevelEq + 1)..].Trim();

                if (key.Equals("filter", StringComparison.OrdinalIgnoreCase))
                {
                    var ast = DslAstParser.Parse(value);
                    parent.Filter = DslFilterConverter.ToFilterGroup(ast);
                    continue;
                }

                if (key.Equals("sort", StringComparison.OrdinalIgnoreCase))
                {
                    parent.Sort = DslSortParser.Parse(value);
                    continue;
                }

                if (!key.Equals("take", StringComparison.OrdinalIgnoreCase))
                    throw new DslParseException(
                        $"Unexpected expand option '{key}'. Expected filter, sort, take, or a nested expand block.");
                
                if (!int.TryParse(value, out var take) || take < 0)
                    throw new DslParseException($"Invalid take value '{value}'. Expected a non-negative integer.");
                parent.Take = take;
                continue;

            }

            var nestedParenIndex = trimmed.IndexOf('(');
            
            if (nestedParenIndex < 0)
                throw new DslParseException(
                    $"Unexpected expand option '{trimmed}'. Expected filter, sort, take, or a nested expand block.");
            var nested = ParseExpandBlock(trimmed);
            parent.Children.Add(nested);
        }
    }

    private static int FindTopLevelEquals(string input)
    {
        var depth = 0;
        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];
            switch (ch)
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case '=' when depth == 0:
                    return i;
            }
        }
        return -1;
    }

    private static List<string> SplitTopLevel(string input, ReadOnlySpan<char> delimiters)
    {
        var result = new List<string>();
        var depth = 0;
        var segmentStart = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];
            switch (ch)
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                default:
                {
                    if (depth == 0 && delimiters.Contains(ch))
                    {
                        result.Add(input[segmentStart..i]);
                        segmentStart = i + 1;
                    }

                    break;
                }
            }
        }

        result.Add(input[segmentStart..]);
        return result;
    }

    private static int FindMatchingCloseParen(string input, int openParenIndex)
    {
        var depth = 0;
        for (var i = openParenIndex; i < input.Length; i++)
        {
            var ch = input[i];
            switch (ch)
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
            }
            if (depth == 0) return i;
        }
        return -1;
    }
}
