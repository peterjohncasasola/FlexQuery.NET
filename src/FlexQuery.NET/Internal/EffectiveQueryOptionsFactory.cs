using FlexQuery.NET.Configuration;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Internal;

/// <summary>
/// Merges global defaults with execution overrides.
/// </summary>
internal static class EffectiveQueryOptionsFactory
{
    /// <summary>
    /// Creates effective query options by merging global defaults with execution overrides.
    /// </summary>
    /// <param name="global">The global FlexQueryOptions configured at application startup.</param>
    /// <param name="execution">The execution-specific options that override global defaults.</param>
    /// <returns>An EffectiveQueryOptions instance with merged configuration.</returns>
    internal static EffectiveQueryOptions Create(
        FlexQueryOptions global,
        QueryExecutionOptions? execution)
    {
        execution ??= new QueryExecutionOptions();

        return new EffectiveQueryOptions
        {
            MaxPageSize = execution.MaxPageSize ?? global.MaxPageSize,
            DefaultPageSize = execution.DefaultPageSize,
            CaseInsensitive = execution.CaseInsensitiveFields,
            IncludeTotalCount = execution.IncludeTotalCount,
            StrictFieldValidation = execution.StrictFieldValidation,
            MaxFieldDepth = execution.MaxFieldDepth ?? global.MaxFieldDepth,
            UseNoTracking = execution.UseNoTracking,
            UseSplitQuery = execution.UseSplitQuery,
            AllowedFields = execution.AllowedFields,
            BlockedFields = execution.BlockedFields,
            AllowedIncludes = execution.AllowedIncludes,
            ExpressionMappings = execution.ExpressionMappings,
            FilterableFields = execution.FilterableFields,
            SortableFields = execution.SortableFields,
            SelectableFields = execution.SelectableFields,
            GroupableFields = execution.GroupableFields,
            AggregatableFields = execution.AggregatableFields,
            DefaultSortField = execution.DefaultSortField,
            DefaultSortDescending = execution.DefaultSortDescending
        };
    }
}