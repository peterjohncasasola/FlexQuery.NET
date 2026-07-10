using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Parsers.Fql;

internal static class FqlFilterConverter
{
    private static readonly HashSet<string> AllowedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "eq", "neq", "gt", "gte", "lt", "lte",
        "contains", "startswith", "endswith", "like",
        "isnull", "isnotnull", "in", "notin", "between",
        "any", "all", "count"
    };

    private static readonly Dictionary<string, string> OperatorAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eq"] = "eq", ["equal"] = "eq", ["equals"] = "eq", ["=="] = "eq", ["="] = "eq",
        ["neq"] = "neq", ["ne"] = "neq", ["notequal"] = "neq", ["!="] = "neq",
        ["gt"] = "gt", ["greaterthan"] = "gt", [">"] = "gt",
        ["gte"] = "gte", ["ge"] = "gte", ["greaterthanorequal"] = "gte", [">="] = "gte",
        ["lt"] = "lt", ["lessthan"] = "lt", ["<"] = "lt",
        ["lte"] = "lte", ["le"] = "lte", ["lessthanorequal"] = "lte", ["<="] = "lte",
        ["contains"] = "contains", ["cn"] = "contains",
        ["like"] = "like",
        ["startswith"] = "startswith", ["starts"] = "startswith", ["sw"] = "startswith",
        ["endswith"] = "endswith", ["ends"] = "endswith", ["ew"] = "endswith",
        ["isnull"] = "isnull", ["null"] = "isnull",
        ["isnotnull"] = "isnotnull", ["notnull"] = "isnotnull", ["isnotempty"] = "isnotnull",
        ["in"] = "in",
        ["notin"] = "notin", ["not in"] = "notin",
        ["between"] = "between",
        ["any"] = "any",
        ["all"] = "all",
        ["count"] = "count"
    };

    public static FilterGroup ToFilterGroup(FqlAstNode node)
    {
        if (node is FqlLogicalNode logical)
            return ConvertLogical(logical);

        var group = new FilterGroup
        {
            Logic = LogicOperator.And
        };

        if (node is FqlCollectionNode collection)
        {
            group.Filters.Add(ConvertCollection(collection));
        }
        else
        {
            var c = (FqlConditionNode)node;
            group.Filters.Add(ConvertCondition(c));
        }

        return group;
    }

    private static FilterGroup ConvertLogical(FqlLogicalNode node)
    {
        var group = new FilterGroup
        {
            Logic = string.Equals(node.Logic.Trim(), "or", StringComparison.OrdinalIgnoreCase)
                ? LogicOperator.Or
                : LogicOperator.And
        };

        foreach (var child in node.Children)
        {
            switch (child)
            {
                case FqlConditionNode condition:
                    group.Filters.Add(ConvertCondition(condition));
                    break;

                case FqlCollectionNode collection:
                    group.Filters.Add(ConvertCollection(collection));
                    break;

                default:
                    group.Groups.Add(ToFilterGroup(child));
                    break;
            }
        }

        return group;
    }

    private static FilterCondition ConvertCollection(FqlCollectionNode node)
    {
        var innerGroup = ToFilterGroup(node.Filter);

        return new FilterCondition
        {
            Field = node.CollectionPath,
            Operator = node.Quantifier,
            Value = null,
            ScopedFilter = innerGroup
        };
    }

    private static FilterCondition ConvertCondition(FqlConditionNode node)
    {
        var op = NormalizeOperator(node.Operator);
        if (!AllowedOperators.Contains(op))
            throw new FqlParseException($"Unsupported FQL operator '{node.Operator}'.");

        var value = op is "in" or "notin" or "between"
            ? string.Join(",", node.Values)
            : node.Values.FirstOrDefault();

        return new FilterCondition
        {
            Field = node.Field,
            Operator = op,
            Value = value
        };
    }

    private static string NormalizeOperator(string? raw)
    {
        var trimmed = (raw ?? string.Empty).Trim();
        return OperatorAliases.TryGetValue(trimmed, out var canonical)
            ? canonical
            : trimmed.ToLowerInvariant();
    }
}
