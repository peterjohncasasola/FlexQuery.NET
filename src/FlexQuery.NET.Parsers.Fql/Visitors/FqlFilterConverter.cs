using FlexQuery.NET.Constants;
using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Parsers.Fql;

internal static class FqlFilterConverter
{
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
        var op = FilterOperators.Normalize(node.Operator);

        var value = op is FilterOperators.In or FilterOperators.NotIn or FilterOperators.Between
            ? string.Join(",", node.Values)
            : node.Values.FirstOrDefault();

        return new FilterCondition
        {
            Field = node.Field,
            Operator = op,
            Value = value
        };
    }
}
