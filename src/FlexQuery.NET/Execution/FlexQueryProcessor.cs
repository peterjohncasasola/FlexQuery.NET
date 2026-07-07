using FlexQuery.NET.Builders;
using FlexQuery.NET.Configurations;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Options;
using FlexQuery.NET.Validation;

namespace FlexQuery.NET.Execution;
/// <inheritdoc/>
public sealed class FlexQueryProcessor(FlexQueryOptions globalOptions) : IFlexQueryProcessor
{
    private static readonly IQueryValidator Validator = new QueryValidator();
    
    /// <inheritdoc />
    public Task<QueryResult<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions options,
        CancellationToken ct = default)
    {
        var execOptions = new QueryExecutionOptions();
        BaseQueryOptions.ApplyGlobalDefaults(execOptions, globalOptions);
        return Task.FromResult(ToTypedResult<T>(ExecuteCore(query, options, execOptions, null)));
    }
    
    /// <inheritdoc />
    public Task<QueryResult<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions options,
        QueryExecutionOptions execOptions,
        CancellationToken ct = default)
    {
        BaseQueryOptions.ApplyGlobalDefaults(execOptions, globalOptions);
        return Task.FromResult(ToTypedResult<T>(ExecuteCore(query, options, execOptions, null)));
    }
    
    /// <inheritdoc />
    public Task<QueryResult<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions options,
        FlexQueryExecutionConfig config,
        CancellationToken ct = default)
    {
        var execOptions = new QueryExecutionOptions();
        BaseQueryOptions.ApplyGlobalDefaults(execOptions, globalOptions);
        return Task.FromResult(ToTypedResult<T>(ExecuteCore(query, options, execOptions, config)));
    }

    internal QueryResult<T> Execute<T>(
        IQueryable<T> query,
        QueryOptions options,
        QueryExecutionOptions execOptions)
    {
        return ToTypedResult<T>(ExecuteCore(query, options, execOptions, null));
    }

    private static QueryResult<T> ToTypedResult<T>(QueryResult<object> result)
    {
        return new QueryResult<T>
        {
            TotalCount = result.TotalCount,
            ResultCount = result.ResultCount,
            Page = result.Page,
            PageSize = result.PageSize,
            Aggregates = result.Aggregates,
            Data = result.Data.Cast<T>().ToList(),
            NextCursorToken = result.NextCursorToken
        };
    }

    private QueryResult<object> ExecuteCore<T>(
        IQueryable<T> query,
        QueryOptions options,
        QueryExecutionOptions execOptions,
        FlexQueryExecutionConfig? config)
    {
        options = options.Normalize();

        ValidatePaginationMode(options);

        var context = new QueryContext
        {
            TargetType = typeof(T),
            ExecutionOptions = execOptions
        };

        var validationResult = Validator.Validate(options, context);
        if (!validationResult.IsValid)
        {
            throw new QueryValidationException(validationResult);
        }

        var filtered = QueryBuilder.ApplyFilter(query, options);
        if (options.Distinct == true)
            filtered = filtered.Distinct();

        var sorted = QueryBuilder.ApplySort(filtered, options);

        var total = TryGetTotalCount(sorted, options, execOptions);

        Dictionary<string, Dictionary<string, object>>? grandTotals = null;
        if (options.Aggregates.Count > 0 &&
            (options.GroupBy == null || options.GroupBy.Count == 0))
        {
            var aggregateQuery = GroupByBuilder.Apply(sorted, options);
            var aggRow = aggregateQuery.FirstOrDefault();
            grandTotals = AggregateResultBuilder.Build(aggRow, options.Aggregates);
        }

        var paged = options.IsKeysetMode
            ? QueryBuilder.ApplyKeysetPaging(sorted, options)
            : QueryBuilder.ApplyPaging(sorted, options);

        if (options.HasProjection())
        {
            var data = QueryBuilder.ApplySelect(paged, options).ToList();
            return options.BuildQueryResult(data, total, grandTotals);
        }

        return options.BuildQueryResult(paged.ToList(), total, grandTotals).ToObjectResult();
    }

    private static void ValidatePaginationMode(QueryOptions options)
    {
        if (!options.IsKeysetMode) return;

        if (options.OffsetExplicitlyRequested)
        {
            throw new QueryValidationException(
                "Offset pagination parameters cannot be used together with Keyset Pagination. " +
                "Choose either Offset Pagination or Keyset Pagination.");
        }
    }

    private static int? TryGetTotalCount<T>(
        IQueryable<T> filteredQuery, QueryOptions options, BaseQueryOptions? execOptions = null)
    {
        if (options.IsKeysetMode)
        {
            return options.IncludeCount == true ? filteredQuery.Count() : null;
        }

        return (options.IncludeCount == true && (execOptions?.IncludeTotalCount ?? true))
            ? filteredQuery.Count()
            : null;
    }
}
