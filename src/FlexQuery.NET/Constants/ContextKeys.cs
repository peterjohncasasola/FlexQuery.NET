namespace FlexQuery.NET.Constants;

/// <summary>
/// Keys used for dictionary lookups in FlexQuery context or options.
/// </summary>
internal static class ContextKeys
{
    public const string EntityType = nameof(EntityType);
    public const string ExpressionMappings = nameof(ExpressionMappings);
    public const string ExecutionOptions = nameof(ExecutionOptions);
    public const string PropertyNameTransformer = nameof(PropertyNameTransformer);

    /// <summary>
    /// Carries the active <see cref="QuerySurface.IQuerySurface"/> (DTO mode) through
    /// <see cref="Models.QueryOptions.Items"/> so every field-resolution consumer
    /// (filter, sort, group, aggregate, projection, keyset paging) resolves public
    /// field names through the same QuerySurface/FieldDescriptor abstraction.
    /// </summary>
    public const string QuerySurface = nameof(QuerySurface);
}
