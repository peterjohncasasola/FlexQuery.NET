using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Parsers.Jql.Ast;

/// <summary>Converts JQL-lite AST nodes into the package's unified filter model.</summary>
public static class JqlFilterConverter
{
    /// <summary>Converts the provided AST node into a FilterGroup.</summary>
    public static FilterGroup ToFilterGroup(JqlAstNode node)
    {
        if (node is JqlLogicalNode logical)
            return ConvertLogical(logical);

        var group = new FilterGroup
        {
            Logic = LogicOperator.And
        };

        if (node is JqlCollectionNode collection)
        {
            group.Filters.Add(ConvertCollection(collection));
        }
        else
        {
            var c = (JqlConditionNode)node;
            group.Filters.Add(ConvertCondition(c));
        }

        return group;
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private static FilterGroup ConvertLogical(JqlLogicalNode node)
    {
        var group = new FilterGroup
        {
            Logic = ParserUtilities.ParseLogic(node.Logic)
        };

        foreach (var child in node.Children)
        {
            switch (child)
            {
                case JqlConditionNode condition:
                    group.Filters.Add(ConvertCondition(condition));
                    break;

                case JqlCollectionNode collection:
                    group.Filters.Add(ConvertCollection(collection));
                    break;

                default:
                    group.Groups.Add(ToFilterGroup(child));
                    break;
            }
        }

        return group;
    }

    /// <summary>
    /// Converts a <see cref="JqlCollectionNode"/> into a <see cref="FilterCondition"/>.
    /// </summary>
    private static FilterCondition ConvertCollection(JqlCollectionNode node)
    {
        var innerGroup = ToFilterGroup(node.Filter);

        return new FilterCondition
        {
            Field        = node.CollectionPath,
            Operator     = node.Quantifier, // "any" or "all"
            Value        = null,
            ScopedFilter = innerGroup
        };
    }

    private static FilterCondition ConvertCondition(JqlConditionNode node)
    {
        var op = FilterOperators.Normalize(node.Operator);
        if (!OperatorRegistry.IsAllowed(op))
            throw new JqlParseException($"Unsupported JQL operator '{node.Operator}'.");

        var value = op is FilterOperators.In or FilterOperators.NotIn or FilterOperators.Between
            ? string.Join(",", node.Values)
            : node.Values.FirstOrDefault();

        return new FilterCondition
        {
            Field    = node.Field,
            Operator = op,
            Value    = value
        };
    }
}
