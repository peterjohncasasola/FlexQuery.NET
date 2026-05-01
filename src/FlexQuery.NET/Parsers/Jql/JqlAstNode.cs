namespace FlexQuery.NET.Parsers.Jql;

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

/// <summary>
/// A scoped collection filter node produced by the <c>orders.any(...)</c> or
/// <c>orders[...]</c> syntaxes. All conditions inside the parentheses / brackets
/// apply to the <strong>same</strong> element of the collection, rather than
/// being independent EXISTS checks.
/// </summary>
public sealed class JqlCollectionNode : JqlAstNode
{
    /// <summary>Creates a new JQL collection node.</summary>
    public JqlCollectionNode(string collectionPath, string quantifier, JqlAstNode filter)
    {
        CollectionPath = collectionPath;
        Quantifier     = quantifier;
        Filter         = filter;
    }

    /// <summary>
    /// Dot-separated path to the collection property (e.g. <c>orders</c> or
    /// <c>customer.orders</c>).
    /// </summary>
    public string CollectionPath { get; }

    /// <summary>The quantifier: <c>any</c> or <c>all</c>.</summary>
    public string Quantifier { get; }

    /// <summary>
    /// The filter expression that every element in the collection is tested
    /// against. This is a fully parsed sub-tree of the same AST types.
    /// </summary>
    public JqlAstNode Filter { get; }
}
