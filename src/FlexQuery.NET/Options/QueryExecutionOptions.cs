namespace FlexQuery.NET.Options;

/// <summary>
/// Concrete Core-only per-request options type.
/// Exists because <see cref="BaseQueryOptions"/> has a protected constructor,
/// providing a way to create Core-only options without depending on a provider package.
/// </summary>
public sealed class QueryExecutionOptions : QueryGovernanceOptions
{
    /// <inheritdoc />
    public QueryExecutionOptions()
    {
        IncludeTotalCount = true;
        DefaultPageSize = 20;
    }
}
