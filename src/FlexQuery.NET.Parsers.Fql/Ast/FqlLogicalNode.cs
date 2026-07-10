namespace FlexQuery.NET.Parsers.Fql;

internal sealed class FqlLogicalNode : FqlAstNode
{
    public FqlLogicalNode(string logic, IReadOnlyList<FqlAstNode> children)
    {
        Logic = logic;
        Children = children;
    }

    public string Logic { get; }
    public IReadOnlyList<FqlAstNode> Children { get; }

    public override string ToString() => $"{Logic.ToUpperInvariant()}({string.Join(", ", Children)})";
}