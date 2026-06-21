namespace FlexQuery.NET.Parsers.Jql;

internal abstract class JqlAstNode
{
}

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

internal sealed class JqlConditionNode : JqlAstNode
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

    public override string ToString() => $"{Field} {Operator} [{string.Join(", ", Values)}]";
}

internal sealed class JqlCollectionNode : JqlAstNode
{
    public JqlCollectionNode(string collectionPath, string quantifier, JqlAstNode filter)
    {
        CollectionPath = collectionPath;
        Quantifier = quantifier;
        Filter = filter;
    }

    public string CollectionPath { get; }
    public string Quantifier { get; }
    public JqlAstNode Filter { get; }

    public override string ToString() => $"{CollectionPath}.{Quantifier}({Filter})";
}
