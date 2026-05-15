using FlexQuery.NET.Models;

namespace FlexQuery.NET.Dapper.Sql.Ast;

/// <summary>
/// AST Node representing an ALL() condition (NOT EXISTS semantics).
/// </summary>
public class AllExpressionNode
{
    public string NavigationProperty { get; set; } = string.Empty;
    public FilterGroup ScopedFilter { get; set; } = new();
}
