namespace FlexQuery.NET.Parsers.Dsl;

/// <summary>A logical AND/OR node with child expressions.</summary>
internal sealed class LogicalNode : DslAstNode
{
    /// <summary>Creates a logical AST node.</summary>
    public LogicalNode(string logic, IReadOnlyList<DslAstNode> children)
    {
        Logic = logic;
        Children = children;
    }

    /// <summary>Logical operator: "and" or "or".</summary>
    public string Logic { get; }

    /// <summary>Child AST nodes.</summary>
    public IReadOnlyList<DslAstNode> Children { get; }
}
