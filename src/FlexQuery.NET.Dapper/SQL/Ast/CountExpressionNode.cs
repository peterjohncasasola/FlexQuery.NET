using FlexQuery.NET.Models;

namespace FlexQuery.NET.Dapper.Sql.Ast;

/// <summary>
/// AST Node representing a COUNT() condition (correlated COUNT semantics).
/// </summary>
internal class CountExpressionNode
{
    /// <summary>The navigation property name on the parent entity.</summary>
    public string NavigationProperty { get; set; } = string.Empty;
    /// <summary>The filter group scoped to the related entity.</summary>
    public FilterGroup ScopedFilter { get; set; } = new();
    /// <summary>The comparison operator, e.g. "=", ">".</summary>
    public string Operator { get; set; } = string.Empty;
    /// <summary>The value to compare the count against.</summary>
    public string? Value { get; set; }
}
