namespace FlexQuery.NET.Parsers.Dsl;

/// <summary>A unary NOT node wrapping a child expression.</summary>
public sealed class NotNode : DslAstNode
{
    /// <summary>Creates a NOT AST node.</summary>
    public NotNode(DslAstNode child)
    {
        Child = child;
    }

    /// <summary>Child AST node to negate.</summary>
    public DslAstNode Child { get; }
}
