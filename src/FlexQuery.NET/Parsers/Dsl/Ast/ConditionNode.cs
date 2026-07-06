namespace FlexQuery.NET.Parsers.Dsl;

/// <summary>A single field/operator/value condition.</summary>
internal sealed class ConditionNode : DslAstNode
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
