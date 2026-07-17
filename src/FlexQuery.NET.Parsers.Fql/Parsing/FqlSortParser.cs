using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Paging;

namespace FlexQuery.NET.Parsers.Fql;

internal static class FqlSortParser
{
    public static List<SortNode> Parse(string? sortRaw)
    {
        if (string.IsNullOrWhiteSpace(sortRaw))
            return [];

        var result = new List<SortNode>();
        var items = ParserUtilities.SplitCsv(sortRaw);

        foreach (var item in items)
        {
            var trimmed = item.Trim();
            if (trimmed.Length == 0)
                throw new FqlParseException(
                    $"Unable to parse sort expression '{sortRaw}'. Empty sort item found. " +
                    $"Expected format: Field [ASC|DESC] or FUNCTION(Field) [ASC|DESC]");

            ParseSortItem(trimmed, result, sortRaw);
        }

        return result;
    }

    private static void ParseSortItem(string item, List<SortNode> result, string rawInput)
    {
        var openParen = item.IndexOf('(');
        var closeParen = item.IndexOf(')');

        if (openParen > 0 && closeParen > openParen)
        {
            ParseAggregateSort(item, openParen, closeParen, result, rawInput);
            return;
        }

        if (openParen > 0 || closeParen > 0)
        {
            throw new FqlParseException(
                $"Unable to parse sort expression '{rawInput}'. Malformed aggregate syntax in '{item}'. " +
                $"Expected format: FUNCTION(Field) [ASC|DESC].");
        }

        ParseFieldSort(item, result, rawInput);
    }

    private static void ParseAggregateSort(string item, int openParen, int closeParen, List<SortNode> result, string rawInput)
    {
        var functionName = item[..openParen].Trim();
        if (functionName.Length == 0)
        {
            throw new FqlParseException(
                $"Unable to parse sort expression '{rawInput}'. Missing aggregate function in '{item}'. " +
                $"Expected format: FUNCTION(Field) [ASC|DESC].");
        }

        AggregateFunction function;
        try
        {
            function = AggregateFunctionConverter.Parse(functionName);
        }
        catch
        {
            throw new FqlParseException(
                $"Unable to parse sort expression '{rawInput}'. Unrecognized aggregate function '{functionName}' in '{item}'. " +
                $"Expected one of: SUM, COUNT, AVG, MIN, MAX.");
        }

        var fieldRaw = item[(openParen + 1)..closeParen].Trim();
        if (fieldRaw.Length == 0)
        {
            throw new FqlParseException(
                $"Unable to parse sort expression '{rawInput}'. Missing field in aggregate '{item}'. " +
                $"Expected format: FUNCTION(Field) [ASC|DESC].");
        }

        if (function == AggregateFunction.Count && fieldRaw == "*")
        {
            throw new FqlParseException(
                $"Unable to parse sort expression '{rawInput}'. COUNT(*) is not supported in sort. Use COUNT(<collection>) or another aggregate over a property instead.");
        }

        if (fieldRaw != "*" && !ParserUtilities.IsValidPropertyPath(fieldRaw.AsSpan()))
        {
            throw new FqlParseException(
                $"Unable to parse sort expression '{rawInput}'. Invalid field path '{fieldRaw}' in aggregate '{item}'. " +
                $"Expected format: FUNCTION(Field) [ASC|DESC].");
        }

        string? field = null;
        string? aggregateField = null;

        if (function == AggregateFunction.Count)
        {
            field = fieldRaw;
        }
        else
        {
            var dotIndex = fieldRaw.LastIndexOf('.');
            if (dotIndex > 0)
            {
                field = fieldRaw[..dotIndex];
                aggregateField = fieldRaw[(dotIndex + 1)..];
            }
            else
            {
                field = fieldRaw;
            }
        }

        var remaining = item[(closeParen + 1)..].Trim();
        var descending = false;

        if (remaining.Length > 0)
        {
            if (remaining.Equals("DESC", StringComparison.OrdinalIgnoreCase) ||
                remaining.Equals("DESCENDING", StringComparison.OrdinalIgnoreCase))
            {
                descending = true;
            }
            else if (remaining.Equals("ASC", StringComparison.OrdinalIgnoreCase) ||
                     remaining.Equals("ASCENDING", StringComparison.OrdinalIgnoreCase))
            {
                descending = false;
            }
            else
            {
                throw new FqlParseException(
                    $"Unable to parse sort expression '{rawInput}'. Invalid sort direction '{remaining}' in '{item}'. " +
                    $"Expected ASC or DESC.");
            }
        }

        result.Add(new SortNode
        {
            Field = field,
            Descending = descending,
            Aggregate = function,
            AggregateField = aggregateField
        });
    }

    private static void ParseFieldSort(string item, List<SortNode> result, string rawInput)
    {
        var lastSpace = item.LastIndexOf(' ');
        if (lastSpace > 0)
        {
            var direction = item[(lastSpace + 1)..].Trim();
            var field = item[..lastSpace].Trim();

            if (direction.Equals("DESC", StringComparison.OrdinalIgnoreCase) ||
                direction.Equals("DESCENDING", StringComparison.OrdinalIgnoreCase))
            {
                if (!ParserUtilities.IsValidPropertyPath(field.AsSpan()))
                    throw new FqlParseException(
                        $"Unable to parse sort expression '{rawInput}'. Invalid field path at '{field}'. " +
                        $"Expected format: Field [ASC|DESC]");
                result.Add(new SortNode { Field = field, Descending = true });
                return;
            }

            if (direction.Equals("ASC", StringComparison.OrdinalIgnoreCase) ||
                direction.Equals("ASCENDING", StringComparison.OrdinalIgnoreCase))
            {
                if (!ParserUtilities.IsValidPropertyPath(field.AsSpan()))
                    throw new FqlParseException(
                        $"Unable to parse sort expression '{rawInput}'. Invalid field path at '{field}'. " +
                        $"Expected format: Field [ASC|DESC]");
                result.Add(new SortNode { Field = field, Descending = false });
                return;
            }
        }

        if (ParserUtilities.IsValidPropertyPath(item.AsSpan()))
        {
            result.Add(new SortNode { Field = item, Descending = false });
        }
        else
        {
            throw new FqlParseException(
                $"Unable to parse sort expression '{rawInput}'. Invalid field path at '{item}'. " +
                $"Expected format: Field [ASC|DESC]");
        }
    }
}