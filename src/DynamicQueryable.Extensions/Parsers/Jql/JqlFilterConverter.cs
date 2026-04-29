using DynamicQueryable.Constants;
using DynamicQueryable.Models;
using DynamicQueryable.Security;

namespace DynamicQueryable.Parsers.Jql;

/// <summary>Converts JQL-lite AST nodes into the package's unified filter model.</summary>
public static class JqlFilterConverter
{
    public static FilterGroup ToFilterGroup(JqlAstNode node)
    {
        if (node is JqlLogicalNode logical)
            return ConvertLogical(logical);

        var c = (JqlConditionNode)node;
        return new FilterGroup
        {
            Logic = LogicOperator.And,
            Filters = [ConvertCondition(c)]
        };
    }

    private static FilterGroup ConvertLogical(JqlLogicalNode node)
    {
        var group = new FilterGroup
        {
            Logic = ParseLogic(node.Logic)
        };

        foreach (var child in node.Children)
        {
            if (child is JqlConditionNode condition)
            {
                group.Filters.Add(ConvertCondition(condition));
                continue;
            }

            group.Groups.Add(ToFilterGroup(child));
        }

        return group;
    }

    private static FilterCondition ConvertCondition(JqlConditionNode node)
    {
        var op = FilterOperators.Normalize(node.Operator);
        if (!OperatorRegistry.IsAllowed(op))
            throw new JqlParseException($"Unsupported JQL operator '{node.Operator}'.");

        var value = op is FilterOperators.In or FilterOperators.NotIn
            ? string.Join(",", node.Values)
            : node.Values.FirstOrDefault();

        return new FilterCondition
        {
            Field = node.Field,
            Operator = op,
            Value = value
        };
    }

    private static LogicOperator ParseLogic(string raw)
        => raw.Equals("or", StringComparison.OrdinalIgnoreCase)
            ? LogicOperator.Or
            : LogicOperator.And;
}

