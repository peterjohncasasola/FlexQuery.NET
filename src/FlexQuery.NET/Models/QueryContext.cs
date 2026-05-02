namespace FlexQuery.NET.Models;

/// <summary>
/// Provides contextual information for the query validation process.
/// This can be extended to include user roles, request metadata, etc.
/// </summary>
public class QueryContext
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
}
