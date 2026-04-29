namespace DynamicQueryable.Parsers.Jql;

/// <summary>Base type for JQL-lite AST nodes.</summary>
public abstract class JqlAstNode
{
}

/// <summary>A logical AND/OR node with child expressions.</summary>
public sealed class JqlLogicalNode : JqlAstNode
{
    public JqlLogicalNode(string logic, IReadOnlyList<JqlAstNode> children)
    {
        Logic = logic;
        Children = children;
    }

    public string Logic { get; }
    public IReadOnlyList<JqlAstNode> Children { get; }
}

/// <summary>A single field/operator/value condition.</summary>
public sealed class JqlConditionNode : JqlAstNode
{
    public JqlConditionNode(string field, string @operator, IReadOnlyList<string> values)
    {
        Field = field;
        Operator = @operator;
        Values = values;
    }

    public string Field { get; }
    public string Operator { get; }
    public IReadOnlyList<string> Values { get; }
}

