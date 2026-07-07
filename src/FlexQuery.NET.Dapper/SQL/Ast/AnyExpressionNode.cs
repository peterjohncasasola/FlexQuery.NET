using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Dapper.Sql.Ast;

/// <summary>
/// AST Node representing an ANY() condition (EXISTS semantics).
/// </summary>
internal sealed class AnyExpressionNode
{
    /// <summary>The navigation property name on the parent entity.</summary>
    public string NavigationProperty { get; set; } = string.Empty;
    /// <summary>The filter group scoped to the related entity.</summary>
    public FilterGroup ScopedFilter { get; set; } = new();
}
