using FlexQuery.NET.Models;

namespace FlexQuery.NET.Execution;

internal static class GroupedQueryExecutor
{
    public static int CountGroupedQuery(
        IQueryable groupedQuery)
    {
        var count = 0;
        foreach (var _ in groupedQuery) count++;
        return count;
    }

    public static IReadOnlyList<object> ExecuteGroupedQuery(
        IQueryable groupedQuery,
        QueryOptions options)
    {
        var data = new List<object>();
        foreach (var item in groupedQuery)
            data.Add(item);
        return data;
    }
}