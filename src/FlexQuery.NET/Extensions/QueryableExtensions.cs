using FlexQuery.NET.Builders;
using FlexQuery.NET.Configuration;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Options;

namespace FlexQuery.NET;

public static class QueryableExtensions
{
    public static IQueryable<T> Apply<T>(this IQueryable<T> query, QueryOptions options)
        => QueryBuilder.Apply(query, options);

    public static IQueryable<T> ApplyFilter<T>(this IQueryable<T> query, QueryOptions options)
        => QueryBuilder.ApplyFilter(query, options);

    public static IQueryable<T> ApplySort<T>(this IQueryable<T> query, QueryOptions options)
        => QueryBuilder.ApplySort(query, options);

    public static IQueryable<T> ApplyPaging<T>(this IQueryable<T> query, QueryOptions options)
        => QueryBuilder.ApplyPaging(query, options);

    public static IQueryable<object> ApplySelect<T>(this IQueryable<T> query, QueryOptions options)
        => QueryBuilder.ApplySelect(query, options);

    public static QueryResult<object> FlexQuery<T>(
        this IQueryable<T> query,
        FlexQueryParameters parameters,
        FlexQueryOptions? global = null,
        Action<QueryExecutionOptions>? configure = null)
    {
        global ??= new FlexQueryOptions();
        var exec = new QueryExecutionOptions();
        global.ApplyTo(exec);
        configure?.Invoke(exec);
        global.Freeze();

        var effectiveSyntax = exec.QuerySyntax ?? global.DefaultQuerySyntax;
        var options = parameters.ToQueryOptions(effectiveSyntax);
        return new FlexQueryProcessor(global).ExecuteAsync(query, options, exec)
            .GetAwaiter().GetResult();
    }

    public static QueryResult<object> FlexQuery<T>(
        this IQueryable<T> query,
        QueryOptions queryOptions,
        FlexQueryOptions? global = null,
        Action<QueryExecutionOptions>? configure = null)
    {
        global ??= new FlexQueryOptions();
        var exec = new QueryExecutionOptions();
        global.ApplyTo(exec);
        configure?.Invoke(exec);
        global.Freeze();

        return new FlexQueryProcessor(global).ExecuteAsync(query, queryOptions, exec)
            .GetAwaiter().GetResult();
    }
}
