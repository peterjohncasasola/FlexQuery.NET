namespace FlexQuery.NET.Parsers.Fql;
using FlexQuery.NET.Models.Filters;

internal sealed class FqlHavingLogicalNode(LogicOperator logic, IReadOnlyList<FqlAstNode> children) : FqlAstNode
{
    public LogicOperator Logic { get; } = logic;
    public IReadOnlyList<FqlAstNode> Children { get; } = children;

    public override string ToString() => $"{Logic.ToKeyword()}({string.Join(", ", Children)})";
}
