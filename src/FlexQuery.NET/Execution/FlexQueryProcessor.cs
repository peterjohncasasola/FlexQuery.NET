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
        var execOptions = CreateExecutionOptions();
        return Task.FromResult(ExecuteCore(query, options, execOptions, null));
    }
    
    /// <inheritdoc />
    public Task<QueryResult<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions options,
        QueryExecutionOptions execOptions,
        CancellationToken ct = default)
    {
        BaseQueryOptions.ApplyGlobalDefaults(execOptions, globalOptions);
        return Task.FromResult(ExecuteCore(query, options, execOptions, null));
    }
    
    /// <inheritdoc />
    public Task<QueryResult<T>> ExecuteAsync<T>(
        IQueryable<T> query,
        QueryOptions options,
        FlexQueryExecutionConfig config,
        CancellationToken ct = default)
    {
        var execOptions = CreateExecutionOptions();
        return Task.FromResult(ExecuteCore(query, options, execOptions, config));
    }
    
    private QueryResult<T> ExecuteCore<T>(
        IQueryable<T> query,
        QueryOptions options,
        QueryExecutionOptions execOptions,
        FlexQueryExecutionConfig? config)
    {
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
        var sorted = QueryBuilder.ApplySort(filtered, options);

        int? totalCount = options.IncludeCount == true ? sorted.Count() : null;

        var paged = QueryBuilder.ApplyPaging(sorted, options);
        var data = paged.ToList();

        return new QueryResult<T>
        {
            Data = (IReadOnlyList<T>)data,
            TotalCount = totalCount,
            Page = options.Paging.Page,
            PageSize = options.Paging.PageSize,
        };
    }

    private QueryExecutionOptions CreateExecutionOptions()
    {
        var options = new QueryExecutionOptions();
        BaseQueryOptions.ApplyGlobalDefaults(options, globalOptions);
        return options;
    }
}
