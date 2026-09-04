using FlexQuery.NET.Options;
using FlexQuery.NET.QuerySurface;

namespace FlexQuery.NET.Execution;

/// <summary>
/// Provides contextual information for the query validation process.
/// This can be extended to include user roles, request metadata, etc.
/// </summary>
public sealed class QueryContext
{
    /// <summary>
    /// Optional metadata associated with the current query context.
    /// Useful for passing custom data to resolvers.
    /// </summary>
    public Dictionary<string, object> Items { get; } = new();

    /// <summary>
    /// Gets or sets the target entity type being queried.
    /// </summary>
    public Type? TargetType { get; set; }

    /// <summary>
    /// Gets or sets the server-side governance and security rules.
    /// </summary>
    public QueryGovernanceOptions? ExecutionOptions { get; set; }

    /// <summary>
    /// Gets or sets the query surface describing DTO/entity field resolution for this request.
    /// Set by the typed DTO execution path before validation.
    /// </summary>
    public IQuerySurface? QuerySurface { get; set; }
}
