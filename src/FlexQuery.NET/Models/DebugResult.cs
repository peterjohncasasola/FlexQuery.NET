namespace FlexQuery.NET.Models;

/// <summary>
/// Contains detailed information about the generated query for debugging purposes.
/// </summary>
public sealed class DebugResult
{
    /// <summary>
    /// The raw AST produced by the parser (JQL, DSL, etc.).
    /// </summary>
    public object? Ast { get; internal set; }

    public string ExpressionTree { get; internal set; } = string.Empty;

    public string LinqLambda { get; internal set; } = string.Empty;
}
