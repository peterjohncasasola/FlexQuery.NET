namespace FlexQuery.NET.Parsers.Dsl;

/// <summary>A relationship filter node (any/all/count) with a scoped filter.</summary>
public sealed class RelationshipNode : DslAstNode
{
    public RelationshipNode(string property, string quantifier, DslAstNode? scopedFilter, string? op = null, string? value = null)
    {
        Property = property;
        Quantifier = quantifier;
        ScopedFilter = scopedFilter;
        Operator = op;
        Value = value;
    }

    public string Property { get; }
    public string Quantifier { get; }
    public DslAstNode? ScopedFilter { get; }
    public string? Operator { get; }
    public string? Value { get; }
}
