using DynamicQueryable.Constants;
using DynamicQueryable.Models;
using DynamicQueryable.Security;

namespace DynamicQueryable.Parsers.Jql;

/// <summary>Converts JQL-lite AST nodes into the package's unified filter model.</summary>
public static class JqlFilterConverter
{
    /// <summary>Converts the provided AST node into a FilterGroup.</summary>
    public static FilterGroup ToFilterGroup(JqlAstNode node)
    {
        if (node is JqlLogicalNode logical)
            return ConvertLogical(logical);

        if (node is JqlCollectionNode collection)
        {
            // A top-level scoped collection node wraps into an AND group with
            // a single FilterCondition that carries the ScopedFilter.
            return new FilterGroup
            {
                Logic   = LogicOperator.And,
                Filters = [ConvertCollection(collection)]
            };
        }

        var c = (JqlConditionNode)node;
        return new FilterGroup
        {
            Logic   = LogicOperator.And,
            Filters = [ConvertCondition(c)]
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private static FilterGroup ConvertLogical(JqlLogicalNode node)
    {
        var group = new FilterGroup
        {
            Logic = ParseLogic(node.Logic)
        };

        foreach (var child in node.Children)
        {
            switch (child)
            {
                case JqlConditionNode condition:
                    group.Filters.Add(ConvertCondition(condition));
                    break;

                case JqlCollectionNode collection:
                    // A scoped collection node becomes a FilterCondition with a
                    // populated ScopedFilter rather than a sub-group, so that the
                    // expression builder can apply all conditions to the SAME element.
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
    /// Converts a <see cref="JqlCollectionNode"/> into a <see cref="FilterCondition"/>
    /// whose <see cref="FilterCondition.ScopedFilter"/> carries the complete inner
    /// filter group. This ensures that all conditions inside <c>orders.any(...)</c>
    /// or <c>orders[...]</c> are evaluated against the <strong>same</strong> element.
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

    private static LogicOperator ParseLogic(string raw)
        => raw.Equals("or", StringComparison.OrdinalIgnoreCase)
            ? LogicOperator.Or
            : LogicOperator.And;
}
