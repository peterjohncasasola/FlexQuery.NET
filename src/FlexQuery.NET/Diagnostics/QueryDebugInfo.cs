namespace FlexQuery.NET.Diagnostics;

/// <summary>
/// Contains detailed information about the generated query for debugging purposes.
/// </summary>
public sealed class QueryDebugInfo
{
    /// <summary>
    /// Gets the raw abstract syntax tree (AST) produced by the parser
    /// (JQL, DSL, AG Grid, Kendo, DataTables, etc.).
    /// </summary>
    public object? Ast { get; internal set; }

    /// <summary>
    /// Gets the generated expression tree in a human-readable format.
    /// </summary>
    public string ExpressionTree { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets the generated LINQ lambda expression in a human-readable format.
    /// </summary>
    public string LinqLambda { get; internal set; } = string.Empty;
}