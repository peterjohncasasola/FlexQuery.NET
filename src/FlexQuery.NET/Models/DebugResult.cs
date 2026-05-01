namespace FlexQuery.NET.Models;

/// <summary>
/// Contains detailed information about the generated query for debugging purposes.
/// </summary>
public sealed class DebugResult
{
    /// <summary>
    /// The raw AST produced by the parser (JQL, DSL, etc.).
    /// </summary>
    public object? Ast { get; set; }

    /// <summary>
    /// The structured representation of the Expression Tree (Internal names and structure).
    /// </summary>
    public string ExpressionTree { get; set; } = string.Empty;

    /// <summary>
    /// The final LINQ lambda string that would be applied to the IQueryable.
    /// </summary>
    public string LinqLambda { get; set; } = string.Empty;
}
