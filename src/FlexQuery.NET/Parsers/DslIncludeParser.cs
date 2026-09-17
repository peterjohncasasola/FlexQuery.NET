using FlexQuery.NET.Parsers.Dsl;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Parses DSL include expressions into <see cref="IncludeAst"/> trees.
/// <para>
/// Grammar: <c>include = entry *( "," entry )</c>, where
/// <c>entry = path [ "(" option *( ";"|"," option ) ")" ]</c> and
/// <c>option = "filter=" expr | "sort=" expr | "take=" int | "include=" entry-list | child-entry</c>.
/// Options inside a block are separated by <c>;</c> (or <c>,</c>). Nested relationships may be
/// written as <c>include=name(...)</c> inside a block or as bare <c>name(...)</c> child blocks;
/// dotted paths (e.g. <c>orders.items</c>) are allowed and merged hierarchically.
/// </para>
/// </summary>
internal static class DslIncludeParser
{
    /// <summary>Parses a DSL include string into a list of <see cref="IncludeAst"/> roots.</summary>
    public static List<IncludeAst> Parse(string? includeRaw)
    {
        return string.IsNullOrWhiteSpace(includeRaw) ? [] : ParseIncludeList(includeRaw);
    }

    private static List<IncludeAst> ParseIncludeList(string input)
    {
        var result = new List<IncludeAst>();
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
                    AddEntry(result, input[blockStart..i].Trim());
                    blockStart = i + 1;
                    break;
            }
        }

        var lastBlock = input[blockStart..].Trim();
        AddEntry(result, lastBlock);

        // A navigation path may be requested at most once per query — reject duplicate
        // full paths deterministically (case-insensitive, consistent with field/path
        // resolution). Merging duplicates would make take/filter/sort ambiguous.
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ast in result)
        {
            var fullPath = string.Join('.', ast.Path);
            if (!seenPaths.Add(fullPath))
                throw new DslParseException(
                    $"Duplicate include path '{fullPath}'. Each navigation path may be included at most once per query.");
        }

        if (result.Count == 0)
            throw new DslParseException("Empty include expression. Expected at least one navigation path.");

        return result;
    }

    private static void AddEntry(List<IncludeAst> result, string entry)
    {
        if (entry.Length == 0)
            throw new DslParseException("Empty include path.");

        var parenIndex = entry.IndexOf('(');
        if (parenIndex < 0)
        {
            var rawSegments = entry.Split('.', StringSplitOptions.TrimEntries);
            if (rawSegments.Length == 0 || rawSegments.Any(s => s.Length == 0) || !AreValidPathSegments(rawSegments))
                throw new DslParseException($"Invalid include path '{entry}'. Expected dot-separated navigation property names.");

            result.Add(new IncludeAst { Path = rawSegments.ToList(), Children = [] });
            return;
        }

        var pathStr = entry[..parenIndex].Trim();
        var pathSegments = pathStr.Split('.', StringSplitOptions.TrimEntries).ToList();
        if (pathSegments.Count == 0 || pathSegments.Any(s => s.Length == 0))
            throw new DslParseException("Empty include path. Expected a navigation property name before the options block.");
        if (!AreValidPathSegments(pathSegments))
            throw new DslParseException(
                $"Invalid include path '{pathStr}'. Expected dot-separated navigation property names.");

        var closeParenIndex = FindMatchingCloseParen(entry, parenIndex);
        if (closeParenIndex < 0)
            throw new DslParseException($"Unclosed parenthesis in include block '{entry}'.");

        var trailing = entry[(closeParenIndex + 1)..].Trim();
        if (trailing.Length > 0)
            throw new DslParseException($"Unexpected trailing input '{trailing}' after include block '{entry}'.");

        var optionsStr = entry[(parenIndex + 1)..closeParenIndex].Trim();
        var ast = new IncludeAst
        {
            Path = pathSegments,
            Children = []
        };
        ParseOptionList(optionsStr, ast);
        result.Add(ast);
    }

    private static void ParseOptionList(string optionsStr, IncludeAst parent)
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
                    throw new DslParseException("Empty include option. Expected filter, sort, take, or include.");
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

                if (key.Equals("include", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var nested in ParseIncludeList(value))
                        parent.Children.Add(nested);
                    continue;
                }

                if (key.Equals("skip", StringComparison.OrdinalIgnoreCase))
                    throw new DslParseException(
                        "The 'skip' option is not supported inside include(...). Use 'take=N' to limit the included collection.");

                if (!key.Equals("take", StringComparison.OrdinalIgnoreCase))
                    throw new DslParseException(
                        $"Unexpected include option '{key}'. Supported options: filter, sort, take, include.");

                if (!int.TryParse(value, out var take) || take < 0)
                    throw new DslParseException($"Invalid take value '{value}'. Expected a non-negative integer.");
                parent.Take = take;
                continue;
            }

            var nestedParenIndex = trimmed.IndexOf('(');
            if (nestedParenIndex < 0)
                throw new DslParseException(
                    $"Unexpected include option '{trimmed}'. Supported options: filter, sort, take, include (=a nested relationship).");

            var nestedEntries = new List<IncludeAst>();
            AddEntry(nestedEntries, trimmed);
            parent.Children.AddRange(nestedEntries);
        }
    }

    private static bool AreValidPathSegments(IReadOnlyList<string> segments)
    {
        foreach (var segment in segments)
        {
            if (segment.Length == 0)
                return false;

            if (!(char.IsLetter(segment[0]) || segment[0] == '_'))
                return false;

            for (var i = 1; i < segment.Length; i++)
            {
                if (!(char.IsLetterOrDigit(segment[i]) || segment[i] == '_'))
                    return false;
            }
        }

        return true;
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
