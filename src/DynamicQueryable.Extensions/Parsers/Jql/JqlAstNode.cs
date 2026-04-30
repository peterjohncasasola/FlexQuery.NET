namespace DynamicQueryable.Parsers.Jql;

/// <summary>Base type for JQL-lite AST nodes.</summary>
public abstract class JqlAstNode
{
}

/// <summary>A logical AND/OR node with child expressions.</summary>
public sealed class JqlLogicalNode : JqlAstNode
{
    /// <summary>Creates a new JQL logical node.</summary>
    public JqlLogicalNode(string logic, IReadOnlyList<JqlAstNode> children)
    {
        Logic = logic;
        Children = children;
    }

    /// <summary>The logical operator (AND / OR).</summary>
    public string Logic { get; }
    /// <summary>The child nodes to combine.</summary>
    public IReadOnlyList<JqlAstNode> Children { get; }
}

/// <summary>A single field/operator/value condition.</summary>
public sealed class JqlConditionNode : JqlAstNode
{
    /// <summary>Creates a new JQL condition node.</summary>
    public JqlConditionNode(string field, string @operator, IReadOnlyList<string> values)
    {
        Field = field;
        Operator = @operator;
        Values = values;
    }

    /// <summary>The field name.</summary>
    public string Field { get; }
    /// <summary>The comparison operator.</summary>
    public string Operator { get; }
    /// <summary>The comparison values.</summary>
    public IReadOnlyList<string> Values { get; }
}

