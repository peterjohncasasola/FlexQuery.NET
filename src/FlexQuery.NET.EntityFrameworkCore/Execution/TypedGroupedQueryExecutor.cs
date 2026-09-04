using FlexQuery.NET.Builders;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.EntityFrameworkCore.Options;
using FlexQuery.NET.Serialization;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;

namespace FlexQuery.NET.EntityFrameworkCore.Execution;

/// <summary>
/// Executes grouped/aggregated FlexQuery queries with typed DTO projection.
/// Reuses the existing GroupByBuilder pipeline and adds TResponse shape validation
/// and projection on top of the dynamic result type.
/// </summary>
internal static class TypedGroupedQueryExecutor
{
    public static async Task<QueryResult<TResponse>> ExecuteAsync<TEntity, TResponse>(
        IQueryable<TEntity> filtered,
        QueryOptions queryOptions,
        int? total,
        EfCoreQueryOptions options,
        CancellationToken cancellationToken)
        where TEntity : class
        where TResponse : class
    {
        var groupedQuery = GroupByBuilder.ApplyUntyped(filtered, queryOptions);
        var dynamicResultType = groupedQuery.ElementType;

        var resultFields = BuildResultFieldMap(queryOptions);

        var listener = options.Listener;
        var ctx = listener is not null
            ? new FlexQueryExecutionContext(listener, cancellationToken)
            : null;

        var resultCount = options.IncludeTotalCount
            ? await GroupedQueryMaterializer.Count(groupedQuery, cancellationToken)
            : (int?)null;

        // Grouped result rows carry group keys plus aggregate aliases. When the response
        // DTO models that shape, project into it; otherwise the rows keep their dynamic
        // grouped shape — aggregate aliases are result metadata, not DTO row properties.
        if (!CanRepresentShape<TResponse>(resultFields.Keys))
        {
            var dynamicResult = await GroupedQueryExecutor.ExecuteAsync(
                filtered, queryOptions, total, options, ctx, cancellationToken);

            return new QueryResult<TResponse>
            {
                Data = DynamicGroupedResult.WrapData<TResponse>(dynamicResult.Data),
                TotalCount = dynamicResult.TotalCount,
                ResultCount = dynamicResult.ResultCount,
                Page = dynamicResult.Page,
                PageSize = dynamicResult.PageSize,
                Aggregates = dynamicResult.Aggregates,
                ResultShape = ResultShapeBuilder.BuildGroupedShape(queryOptions)
            };
        }

        var projection = BuildTypedGroupProjection(dynamicResultType, typeof(TResponse), resultFields);
        var typedQuery = ApplyTypedGroupProjection<TResponse>(groupedQuery, projection);

        var (sql, queryParameters) = SqlQueryInspector.TryGetSqlWithParameters(typedQuery);

        if (ctx is not null)
            await ctx.NotifyTranslatedAsync(sql, queryParameters);

        var data = await typedQuery.ToListAsync(cancellationToken);

        var resultShape = ResultShapeBuilder.BuildGroupedShape(queryOptions);

        var result = new QueryResult<TResponse>
        {
            Data = data,
            TotalCount = total,
            ResultCount = resultCount,
            Page = queryOptions.Paging.Page > 0 ? queryOptions.Paging.Page : 1,
            PageSize = queryOptions.Paging.PageSize > 0 ? queryOptions.Paging.PageSize : options.DefaultPageSize,
            ResultShape = resultShape
        };

        if (ctx is not null)
        {
            await ctx.NotifyExecutedAsync(data.Count);
            await ctx.NotifyMaterializedAsync(result);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, Type> BuildResultFieldMap(QueryOptions queryOptions)
    {
        var map = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in from groupField in queryOptions.GroupBy ?? [] select GroupByBuilder.GetProjectionName(groupField))
        {
            map[name] = typeof(object);
        }

        foreach (var aggregate in queryOptions.Aggregates)
        {
            map[aggregate.Alias] = typeof(object);
        }

        return map;
    }

    /// <summary>
    /// Returns true when the response DTO models the full grouped result shape —
    /// every group key and aggregate alias has a writable property. Grouped rows are a
    /// distinct result shape from the row DTO; when the DTO does not model it, the
    /// dynamic grouped shape is used instead of forcing aggregate aliases onto the DTO.
    /// </summary>
    private static bool CanRepresentShape<TResponse>(IEnumerable<string> resultFieldNames)
        where TResponse : class
    {
        var responseType = typeof(TResponse);
        var writableProps = responseType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        return resultFieldNames.All(writableProps.ContainsKey);
    }

    private static LambdaExpression BuildTypedGroupProjection(Type sourceType, Type responseType, IReadOnlyDictionary<string, Type> resultFields)
    {
        var param = Expression.Parameter(sourceType, "x");
        var bindings = new List<MemberBinding>();

        var responseProps = responseType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        foreach (var field in resultFields.Keys)
        {
            if (!responseProps.TryGetValue(field, out var responseProp))
                continue;

            var sourceProp = sourceType.GetProperty(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (sourceProp is null)
                continue;

            Expression sourceAccess = Expression.Property(param, sourceProp);
            if (sourceAccess.Type != responseProp.PropertyType)
            {
                sourceAccess = Expression.Convert(sourceAccess, responseProp.PropertyType);
            }
            var binding = Expression.Bind(responseProp, sourceAccess);
            bindings.Add(binding);
        }

        var body = Expression.MemberInit(Expression.New(responseType), bindings);
        return Expression.Lambda(body, param);
    }

    private static IQueryable<TResponse> ApplyTypedGroupProjection<TResponse>(IQueryable groupedQuery, LambdaExpression projection)
        where TResponse : class
    {
        var selectMethod = typeof(Queryable)
            .GetMethods()
            .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
            .MakeGenericMethod(groupedQuery.ElementType, typeof(TResponse));

        var call = Expression.Call(selectMethod, groupedQuery.Expression, projection);
        return groupedQuery.Provider.CreateQuery<TResponse>(call);
    }
}
