using FlexQuery.NET.Builders;
using FlexQuery.NET.Configurations;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Options;
using FlexQuery.NET.Validation;

namespace FlexQuery.NET.Execution;
/// <inheritdoc/>
public sealed class FlexQueryProcessor(FlexQueryOptions globalOptions) : IFlexQueryProcessor
{
    private static readonly IQueryValidator Validator = new QueryValidator();
    
    /// <inheritdoc />
    public async Task<QueryResult<object>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions queryOptions,
        CancellationToken ct = default)
    {
        var execOptions = new QueryExecutionOptions();
        BaseQueryOptions.ApplyGlobalDefaults(execOptions, globalOptions);

        return await ExecuteCore(query, queryOptions, execOptions, ct);

    }
    
    /// <inheritdoc />
    public async Task<QueryResult<object>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions queryOptions,
        QueryExecutionOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new  QueryExecutionOptions();
        BaseQueryOptions.ApplyGlobalDefaults(options, globalOptions);

        return await ExecuteCore(query, queryOptions, options, ct);

    }
    
    private static async Task<QueryResult<object>> ExecuteCore<T>(
        IQueryable<T> query,
        QueryOptions queryOptions,
        QueryExecutionOptions options,
        CancellationToken cancellationToken = default)
    {
        queryOptions = queryOptions.Normalize();

        var listener = options.Listener;
        
        var executionContext = listener is not null
            ? new FlexQueryExecutionContext(listener, cancellationToken)
            : null;

        IReadOnlyList<object>? dataList = null;
        QueryResult<object>? result;

        if (options.ExpressionMappings != null)
        {
            queryOptions.Items[ContextKeys.ExpressionMappings] = options.ExpressionMappings;
        }

        var context = new QueryContext
        {
            TargetType = typeof(T),
            ExecutionOptions = options
        };

        var validationResult = Validator.Validate(queryOptions, context);
        if (!validationResult.IsValid)
        {
            throw new QueryValidationException(validationResult);
        }

        var filtered = QueryBuilder.ApplyFilter(query, queryOptions);
        if (queryOptions.Distinct == true)
            filtered = filtered.Distinct();


        var total = TryGetTotalCount(filtered, queryOptions, options);

        if (queryOptions.GroupBy is { Count: > 0 })
        {
            var groupedQuery = GroupByBuilder.ApplyUntyped(filtered, queryOptions);

            var resultCount = options.IncludeTotalCount ? 
                GroupedQueryExecutor.CountGroupedQuery(groupedQuery) : (int?)null;
            
            var data = GroupedQueryExecutor.ExecuteGroupedQuery(groupedQuery, queryOptions);
            var groupResult = queryOptions.BuildQueryResult(data, total, resultCount: resultCount);
            
            if (executionContext?.Listener is null) return groupResult;

            await executionContext.NotifyExecutedAsync(data.Count);
            await executionContext.NotifyMaterializedAsync(groupResult);
            return groupResult;
        }
        
        filtered = QueryBuilder.ApplySort(filtered, queryOptions);

        Dictionary<string, Dictionary<string, object>>? grandTotals = null;
        if (queryOptions.Aggregates.Count > 0 &&
            (queryOptions.GroupBy == null || queryOptions.GroupBy.Count == 0))
        {
            var aggregateQuery = GroupByBuilder.Apply(filtered, queryOptions);
            var aggRow = aggregateQuery.FirstOrDefault();
            grandTotals = AggregateResultBuilder.Build(aggRow, queryOptions.Aggregates);
        }
        

        filtered = queryOptions.IsKeysetMode 
            ? QueryBuilder.ApplyKeysetPaging(filtered, queryOptions) 
            : QueryBuilder.ApplyPaging(filtered, queryOptions);

        try
        {
            if (queryOptions.HasProjection())
            {
                var projectedData = filtered.ApplySelect(queryOptions).ToList();
                dataList = projectedData;

                if (executionContext?.Listener is not null)
                {
                    await executionContext.NotifyExecutedAsync(projectedData.Count);
                }
                
                result = queryOptions.BuildQueryResult(projectedData, total, grandTotals).ToObjectResult();
                
            }
            else
            {
                var filteredData = filtered.ToList();
                dataList = (IReadOnlyList<object>?)filteredData;
                
                if (executionContext?.Listener is not null)
                {
                    await executionContext.NotifyExecutedAsync(filteredData.Count);
                }
                
                result = queryOptions.BuildQueryResult(filteredData, total, grandTotals).ToObjectResult();
            }
            

        }
        catch (Exception exception)
        {
            if (executionContext?.Listener is null) throw;
            
            if (dataList is not null)
            {
                await executionContext.NotifyMaterializedAsync(null, exception);
            }
            else
            {
                await executionContext.NotifyExecutedAsync(null, exception);
            }
            throw;
        }

        return result;

    }

    private static int? TryGetTotalCount<T>(
        IQueryable<T> filteredQuery, QueryOptions options, BaseQueryOptions? execOptions = null)
    {
        if (options.IsKeysetMode)
        {
            return options.IncludeCount == true ? filteredQuery.Count() : null;
        }

        return options.IncludeCount == true && (execOptions?.IncludeTotalCount ?? true)
            ? filteredQuery.Count()
            : null;
    }
}
