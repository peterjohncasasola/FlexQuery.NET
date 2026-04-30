using DynamicQueryable.Constants;
using DynamicQueryable.Models;

namespace DynamicQueryable.Parsers.Dsl;

/// <summary>Converts DSL AST nodes into the package's unified filter model.</summary>
public static class DslFilterConverter
{
    /// <summary>Converts a DSL AST into a <see cref="FilterGroup"/>.</summary>
    public static FilterGroup ToFilterGroup(DslAstNode node)
    {
        if (node is NotNode not)
        {
            var negatedGroup = ToFilterGroup(not.Child);
            negatedGroup.IsNegated = !negatedGroup.IsNegated;
            return negatedGroup;
        }

        if (node is LogicalNode logical)
            return ConvertLogical(logical);

        return new FilterGroup
        {
            Logic = LogicOperator.And,
            Filters = [ConvertCondition((ConditionNode)node)]
        };
    }

    private static FilterGroup ConvertLogical(LogicalNode node)
    {
        var group = new FilterGroup
        {
            Logic = ParseLogic(node.Logic)
        };

        foreach (var child in node.Children)
        {
            if (child is ConditionNode condition)
            {
                group.Filters.Add(ConvertCondition(condition));
                continue;
            }

            group.Groups.Add(ToFilterGroup(child));
        }

        return group;
    }

    private static FilterCondition ConvertCondition(ConditionNode node)
        => new()
        {
            Field = node.Field,
            Operator = FilterOperators.Normalize(node.Operator),
            Value = node.Value
        };

    private static LogicOperator ParseLogic(string logic)
        => logic.Equals("or", StringComparison.OrdinalIgnoreCase)
            ? LogicOperator.Or
            : LogicOperator.And;
}
