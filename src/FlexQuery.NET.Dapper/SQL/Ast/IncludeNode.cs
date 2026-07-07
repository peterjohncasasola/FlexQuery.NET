using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Dapper.Sql.Ast;

/// <summary>
/// AST Node representing an include (LEFT JOIN) semantics.
/// </summary>
internal class IncludeNode
{
    /// <summary>The navigation property name on the parent entity.</summary>
    public string NavigationProperty { get; set; } = string.Empty;
    /// <summary>Optional filter applied to the included entity.</summary>
    public FilterGroup? Filter { get; set; }
}
