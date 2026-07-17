namespace FlexQuery.NET.Parsers.Fql;

/// <summary>A single field/operator/value condition in the FQL AST.</summary>
/// <remarks>
/// The <see cref="Operator"/> is guaranteed to be a supported FQL operator.
/// The parser rejects unsupported operators before constructing this node.
/// </remarks>
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