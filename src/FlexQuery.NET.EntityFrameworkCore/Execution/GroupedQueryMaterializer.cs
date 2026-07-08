using System.Reflection;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Models;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.EntityFrameworkCore.Execution;

/// <summary>
/// Invokes the generic, EF-Core-typed count/materialize calls for a grouped
/// query whose shape type is only known at runtime, via cached reflection.
/// </summary>
internal static class GroupedQueryMaterializer
{
    private static readonly MethodInfo ExecuteMethod = typeof(GroupedQueryMaterializer)
        .GetMethod(nameof(ExecuteAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo CountMethod = typeof(GroupedQueryMaterializer)
        .GetMethod(nameof(CountAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static Task<IReadOnlyList<object>> Execute(IQueryable groupedQuery, QueryOptions options, CancellationToken cancellationToken)
    {
        return (Task<IReadOnlyList<object>>)ExecuteMethod
            .MakeGenericMethod(groupedQuery.ElementType)
            .Invoke(null, [groupedQuery, options, cancellationToken])!;
    }

    public static Task<int> Count(IQueryable groupedQuery, CancellationToken cancellationToken)
    {
        return (Task<int>)CountMethod
            .MakeGenericMethod(groupedQuery.ElementType)
            .Invoke(null, [groupedQuery, cancellationToken])!;
    }

    private static Task<int> CountAsync<TShape>(IQueryable groupedQuery, CancellationToken cancellationToken)
    {
        return ((IQueryable<TShape>)groupedQuery).CountAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<object>> ExecuteAsync<TShape>(
        IQueryable groupedQuery,
        QueryOptions options,
        CancellationToken cancellationToken)
    {
        var typedQuery = GroupedQueryBuilder.Apply<TShape>(groupedQuery, options);

        var rows = await typedQuery.ToListAsync(cancellationToken);
        return rows.Cast<object>().ToList();
    }
}