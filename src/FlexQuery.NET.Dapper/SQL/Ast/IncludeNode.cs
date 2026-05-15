using FlexQuery.NET.Models;

namespace FlexQuery.NET.Dapper.Sql.Ast;

/// <summary>
/// AST Node representing an include (LEFT JOIN) semantics.
/// </summary>
public class IncludeNode
{
    public string NavigationProperty { get; set; } = string.Empty;
    public FilterGroup? Filter { get; set; }
}
