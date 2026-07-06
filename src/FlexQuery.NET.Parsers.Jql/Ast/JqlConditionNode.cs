namespace FlexQuery.NET.Parsers.Jql;

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