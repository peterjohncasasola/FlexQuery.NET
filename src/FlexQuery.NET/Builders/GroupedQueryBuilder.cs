using FlexQuery.NET.Models;
using FlexQuery.NET.Validation;

namespace FlexQuery.NET.Builders;

internal static class GroupedQueryBuilder
{
    public static IQueryable<TShape> Apply<TShape>(IQueryable groupedQuery, QueryOptions options)
    {
        var typedQuery = (IQueryable<TShape>)groupedQuery;
            
        var sorts = GroupedSortValidator.ValidateOrThrow(
            options.Sort,
            options.GroupBy ?? [],
            options.Aggregates);

        typedQuery = QueryBuilder.ApplySort(typedQuery, sorts, options);
        typedQuery = QueryBuilder.ApplyPaging(typedQuery, options);

        return typedQuery;
    }
}