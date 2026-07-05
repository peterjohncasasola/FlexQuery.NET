using FlexQuery.NET.Builders;
using FlexQuery.NET.Caching;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Validation;

namespace FlexQuery.NET;

/// <summary>
/// Extensions for <see cref="QueryOptions"/>.
/// </summary>
public static class QueryOptionsExtensions
{
    /// <summary>
    /// Validates the query options or throws a <see cref="QueryValidationException"/>.
    /// </summary>
    /// <typeparam name="T">The entity type that the query targets.</typeparam>
    /// <param name="options">The query options to validate.</param>
    /// <param name="execOptions">Optional execution options that define server-side constraints.</param>
    /// <exception cref="QueryValidationException">Thrown when validation fails.</exception>
    public static void ValidateOrThrow<T>(
        this QueryOptions options,
        QueryExecutionOptions? execOptions = null)
    {
        options.ValidateOrThrow(typeof(T), execOptions);
    }

    /// <summary>
    /// Validates the query options or throws a <see cref="QueryValidationException"/>.
    /// </summary>
    /// <param name="options">The query options to validate.</param>
    /// <param name="entityType">The entity type that the query targets.</param>
    /// <param name="execOptions">Optional execution options that define server-side constraints.</param>
    /// <exception cref="QueryValidationException">Thrown when validation fails.</exception>
    public static void ValidateOrThrow(
        this QueryOptions options,
        Type entityType,
        QueryExecutionOptions? execOptions = null)
    {
        execOptions ??= new QueryExecutionOptions();

        if (execOptions.ExpressionMappings != null)
        {
            options.Items[ContextKeys.ExpressionMappings] = execOptions.ExpressionMappings;
        }

        var result = options.Validate(entityType, execOptions);

        if (!result.IsValid)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Message));
            throw new QueryValidationException($"Query validation failed: {errors}", result);
        }
    }

    /// <summary>
    /// Validates the query options and returns the result safely.
    /// </summary>
    /// <typeparam name="T">The entity type that the query targets.</typeparam>
    /// <param name="options">The query options to validate.</param>
    /// <param name="execOptions">Optional execution options that define server-side constraints.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure with details.</returns>
    public static ValidationResult ValidateSafe<T>(
        this QueryOptions options,
        QueryExecutionOptions? execOptions = null)
    {
        return ValidateInternal<T>(options, execOptions);
    }

    private static ValidationResult ValidateInternal<T>(
        QueryOptions options,
        QueryExecutionOptions? execOptions)
    {
        execOptions ??= new QueryExecutionOptions();

        if (execOptions.ExpressionMappings != null)
        {
            options.Items[ContextKeys.ExpressionMappings] = execOptions.ExpressionMappings;
        }

        var result = options.Validate(typeof(T), execOptions);

        return result;
    }

    /// <summary>
    /// Wraps data into a <see cref="QueryResult{T}"/>.
    /// </summary>
    /// <typeparam name="T">The element type of the data collection.</typeparam>
    /// <param name="options">The query options that contributed to the result.</param>
    /// <param name="data">The collection of result items.</param>
    /// <param name="totalCount">Optional total count of all items before paging.</param>
    /// <param name="aggregates">Optional grand total aggregate results.</param>
    /// <param name="resultCount">Optional count of rows after shaping, before paging.</param>
    /// <returns>A <see cref="QueryResult{T}"/> containing the data and pagination metadata.</returns>
    public static QueryResult<T> BuildQueryResult<T>(
        this QueryOptions options,
        IEnumerable<T> data,
        int? totalCount = null,
        Dictionary<string, Dictionary<string, object>>? aggregates = null,
        int? resultCount = null)
    {
        return new QueryResult<T>
        {
            TotalCount = totalCount,
            ResultCount = resultCount ?? totalCount,
            Page       = options.Paging.Page,
            PageSize   = options.Paging.PageSize,
            Aggregates = aggregates,
            Data       = data.ToList()
        };
    }

    /// <summary>
    /// Checks if the query options contain any explicit projection.
    /// </summary>
    /// <param name="options">The query options to check.</param>
    /// <returns><c>true</c> if the options specify a projection; otherwise, <c>false</c>.</returns>
    internal static bool HasProjection(this QueryOptions options)
    {
        return (options.Select?.Count ?? 0) > 0
               || options.SelectTree is not null
               || (options.Includes?.Count ?? 0) > 0
               || (options.Expand?.Count ?? 0) > 0
               || (options.GroupBy?.Count ?? 0) > 0
               || options.Aggregates.Count > 0;
    }

    /// <summary>
    /// Normalizes the query options into a canonical form for deterministic cache keys,
    /// semantic comparison, and consistent pipeline processing.
    /// </summary>
    /// <remarks>
    /// Returns a new <see cref="QueryOptions"/> instance with normalized state.
    /// The original instance is not modified.
    /// Performs the following normalizations:
    /// <list type="bullet">
    ///   <item>Filter AST canonicalization</item>
    ///   <item>Includes → FilteredIncludes consolidation</item>
    /// </list>
    /// </remarks>
    /// <param name="options">The query options to normalize.</param>
    /// <returns>A new <see cref="QueryOptions"/> instance with normalized state.</returns>
    public static QueryOptions Normalize(this QueryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = CopyQueryOptions(options);

        // 1. Filter AST normalization
        result.Filter = FilterNormalizer.Normalize(result.Filter);

        // 2. Includes → FilteredIncludes consolidation
        if (result.Includes?.Count > 0)
        {
            if (result.Expand == null)
            {
                result.Expand = result.Includes
                    .Select(path => new IncludeNode { Path = path })
                    .ToList();
            }
            else
            {
                if (result.Expand != null)
                {
                    var existingPathSet = new HashSet<string>(
                        result.Expand.Select(i => i.Path),
                        StringComparer.OrdinalIgnoreCase);
                    foreach (var inc in result.Includes)
                    {
                        if (!existingPathSet.Contains(inc))
                        {
                            result.Expand.Add(new IncludeNode { Path = inc });
                            existingPathSet.Add(inc);
                        }
                    }
                }
            }
            result.Includes = null;
        }

        return result;
    }

    internal static QueryOptions CopyQueryOptions(this QueryOptions source)
    {
        var copy = new QueryOptions
        {
            Filter = CopyFilterGroup(source.Filter),
            Sort = source.Sort.Select(CloneSortNode).ToList(),
            Select = source.Select?.ToList(),
            Includes = source.Includes?.ToList(),
            Expand = source.Expand?.Select(CloneIncludeNode).ToList(),
            ProjectionMode = source.ProjectionMode,
            GroupBy = source.GroupBy?.ToList(),
            Aggregates = source.Aggregates.Select(CloneAggregateModel).ToList(),
            Having = CloneHavingCondition(source.Having),
            Distinct = source.Distinct,
            SelectTree = source.SelectTree,
            Paging = new PagingOptions { Page = source.Paging.Page, PageSize = source.Paging.PageSize, Disabled = source.Paging.Disabled },
            IncludeCount = source.IncludeCount,
            CaseInsensitive = source.CaseInsensitive,
            EnableCache = source.EnableCache,
            UseEfCoreOperators = source.UseEfCoreOperators
        };

        foreach (var kv in source.Items)
        {
            copy.Items[kv.Key] = kv.Value;
        }

        return copy;
    }

    private static FilterGroup? CopyFilterGroup(FilterGroup? group)
    {
        if (group is null) return null;
        return new FilterGroup
        {
            Logic = group.Logic,
            IsNegated = group.IsNegated,
            Filters = group.Filters.Select(f => new FilterCondition
            {
                Field = f.Field,
                Operator = f.Operator,
                Value = f.Value,
                IsNegated = f.IsNegated,
                ScopedFilter = CopyFilterGroup(f.ScopedFilter)
            }).ToList(),
            Groups = group.Groups.Select(g => CopyFilterGroup(g)!).ToList()
        };
    }

    private static SortNode CloneSortNode(SortNode sort)
        => new()
        {
            Field = sort.Field,
            Aggregate = sort.Aggregate,
            AggregateField = sort.AggregateField,
            Descending = sort.Descending
        };

    private static IncludeNode CloneIncludeNode(IncludeNode include)
        => new()
        {
            Path = include.Path,
            Filter = CopyFilterGroup(include.Filter),
            Children = include.Children.Select(CloneIncludeNode).ToList()
        };

    private static AggregateModel CloneAggregateModel(AggregateModel aggregate)
        => new()
        {
            Function = aggregate.Function,
            Field = aggregate.Field,
            Alias = aggregate.Alias
        };

    private static HavingCondition? CloneHavingCondition(HavingCondition? having)
        => having is null
            ? null
            : new HavingCondition
            {
                Function = having.Function,
                Field = having.Field,
                Operator = having.Operator,
                Value = having.Value
            };

    /// <summary>
    /// Generates a stable cache key for the query execution pipeline.
    /// </summary>
    /// <param name="options">The query options to generate a key for.</param>
    /// <param name="entityType">The entity type being queried.</param>
    /// <param name="operation">The name of the query operation (e.g., "predicate", "projection").</param>
    /// <returns>A string representing the cache key for this query configuration.</returns>
    public static string GetCacheKey(
        this QueryOptions options,
        Type entityType,
        string operation)
    {
        return QueryCacheKeyBuilder.Build(options, entityType, operation);
    }
}
