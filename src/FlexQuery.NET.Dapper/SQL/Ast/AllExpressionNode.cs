using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Dapper.Sql.Ast;

/// <summary>
/// AST Node representing an ALL() condition (NOT EXISTS semantics).
/// </summary>
internal class AllExpressionNode
{
    /// <summary>The navigation property name on the parent entity.</summary>
    public string NavigationProperty { get; set; } = string.Empty;
    /// <summary>The filter group scoped to the related entity.</summary>
    public FilterGroup ScopedFilter { get; set; } = new();
}
