namespace FlexQuery.NET.Parsers.Fql;

internal sealed class FqlConditionNode : FqlAstNode
{
    public FqlConditionNode(string field, string @operator, IReadOnlyList<string> values)
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