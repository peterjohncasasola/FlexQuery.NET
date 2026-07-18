using FlexQuery.NET.Constants;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Parsers.Fql;

internal static class FqlHavingParser
{
    public static HavingNode? Parse(string? rawHaving)
    {
        if (string.IsNullOrWhiteSpace(rawHaving))
            return null;

        var trimmed = rawHaving.Trim();

        if (trimmed.Length == 0) return null;

        var ast = FqlHavingAstParser.Parse(trimmed);
        return Convert(ast);
    }

    private static HavingNode Convert(FqlAstNode node)
        => node switch
        {
            FqlHavingConditionNode c => new HavingConditionNode
            {
                Function = c.Function,
                Field = string.IsNullOrWhiteSpace(c.Field) ? null : c.Field,
                Operator = FilterOperators.Normalize(c.Operator),
                Value = c.Value
            },
            FqlHavingLogicalNode logicalNode => new HavingLogicalNode
            {
                Logic = logicalNode.Logic,
                Children = logicalNode.Children.Select(Convert).ToList()
            },
            FqlHavingGroupNode g => new HavingGroupNode
            {
                Inner = Convert(g.Inner)
            },
            _ => throw new FqlParseException($"Unsupported HAVING AST node type: {node.GetType().Name}.")
        };
}
