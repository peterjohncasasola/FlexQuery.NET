namespace FlexQuery.NET.Parsers.Jql;

internal sealed class JqlLogicalNode : JqlAstNode
{
    public JqlLogicalNode(string logic, IReadOnlyList<JqlAstNode> children)
    {
        Logic = logic;
        Children = children;
    }

    public string Logic { get; }
    public IReadOnlyList<JqlAstNode> Children { get; }

    public override string ToString() => $"{Logic.ToUpperInvariant()}({string.Join(", ", Children)})";
}