using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Parsers.Dsl;

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

        if (node is RelationshipNode rel)
        {
            return new FilterGroup
            {
                Logic = LogicOperator.And,
                Filters = [ConvertRelationship(rel)]
            };
        }

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
            Logic = ParserUtilities.ParseLogic(node.Logic)
        };

        foreach (var child in node.Children)
        {
            if (child is ConditionNode condition)
            {
                group.Filters.Add(ConvertCondition(condition));
                continue;
            }

            if (child is RelationshipNode rel)
            {
                group.Filters.Add(ConvertRelationship(rel));
                continue;
            }

            group.Groups.Add(ToFilterGroup(child));
        }

        return group;
    }

    private static FilterCondition ConvertRelationship(RelationshipNode node)
    {
        var cond = new FilterCondition
        {
            Field = node.Property,
            Operator = node.Quantifier.ToLowerInvariant(),
            ScopedFilter = node.ScopedFilter != null ? ToFilterGroup(node.ScopedFilter) : null
        };

        if (node.Operator != null)
        {
            cond.Value = $"{node.Operator}:{node.Value}";
        }

        return cond;
    }

    private static FilterCondition ConvertCondition(ConditionNode node)
        => new()
        {
            Field = node.Field,
            Operator = FilterOperators.Normalize(node.Operator),
            Value = node.Value
        };
}
