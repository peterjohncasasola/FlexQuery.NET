namespace DynamicQueryable.Parsers.Dsl;

/// <summary>Base type for DSL filter AST nodes.</summary>
public abstract class DslAstNode
{
}

/// <summary>A single field/operator/value condition.</summary>
public sealed class ConditionNode : DslAstNode
{
    /// <summary>Creates a condition AST node.</summary>
    public ConditionNode(string field, string @operator, string? value)
    {
        Field = field;
        Operator = @operator;
        Value = value;
    }

    /// <summary>Field or nested property path.</summary>
    public string Field { get; }

    /// <summary>Filter operator.</summary>
    public string Operator { get; }

    /// <summary>Raw string value, when the operator requires one.</summary>
    public string? Value { get; }
}

/// <summary>A logical AND/OR node with child expressions.</summary>
public sealed class LogicalNode : DslAstNode
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
