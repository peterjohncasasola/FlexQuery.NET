using FlexQuery.NET.Models;

namespace FlexQuery.NET.Dapper.Sql.Ast;

/// <summary>
/// AST Node representing a COUNT() condition (correlated COUNT semantics).
/// </summary>
public class CountExpressionNode
{
    public string NavigationProperty { get; set; } = string.Empty;
    public FilterGroup ScopedFilter { get; set; } = new();
    public string Operator { get; set; } = string.Empty;
    public string? Value { get; set; }
}
