using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Parsers.Fql;

internal sealed class FqlHavingConditionNode(AggregateFunction function, string field, string @operator, string value)
    : FqlAstNode
{
    public AggregateFunction Function { get; } = function;
    public string Field { get; } = field;
    public string Operator { get; } = @operator;
    public string Value { get; } = value;

    public override string ToString() => $"{Function.ToKeyword().ToUpperInvariant()}({Field}) {Operator} {Value}";
}
