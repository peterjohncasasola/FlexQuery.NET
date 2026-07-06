namespace FlexQuery.NET.Parsers.Dsl;

/// <summary>A relationship filter node (any/all/count) with a scoped filter.</summary>
internal sealed class RelationshipNode : DslAstNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RelationshipNode"/> class.
    /// </summary>
    /// <param name="property">The property name for the relationship.</param>
    /// <param name="quantifier">The quantifier (any, all, count).</param>
    /// <param name="scopedFilter">The nested scoped filter node.</param>
    /// <param name="op">Optional comparison operator for count quantifiers.</param>
    /// <param name="value">Optional comparison value for count quantifiers.</param>
    public RelationshipNode(string property, string quantifier, DslAstNode? scopedFilter, string? op = null, string? value = null)
    {
        Property = property;
        Quantifier = quantifier;
        ScopedFilter = scopedFilter;
        Operator = op;
        Value = value;
    }

    /// <summary>Gets the property name for the relationship.</summary>
    public string Property { get; }
    /// <summary>Gets the quantifier (any, all, count).</summary>
    public string Quantifier { get; }
    /// <summary>Gets the nested scoped filter node.</summary>
    public DslAstNode? ScopedFilter { get; }
    /// <summary>Gets the optional comparison operator for count quantifiers.</summary>
    public string? Operator { get; }
    /// <summary>Gets the optional comparison value for count quantifiers.</summary>
    public string? Value { get; }
}

