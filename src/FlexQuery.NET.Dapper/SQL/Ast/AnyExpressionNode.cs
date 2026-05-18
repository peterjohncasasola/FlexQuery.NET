using FlexQuery.NET.Models;

namespace FlexQuery.NET.Dapper.Sql.Ast;

/// <summary>
/// AST Node representing an ANY() condition (EXISTS semantics).
/// </summary>
public class AnyExpressionNode
{
    public string NavigationProperty { get; set; } = string.Empty;
    public FilterGroup ScopedFilter { get; set; } = new();
}
