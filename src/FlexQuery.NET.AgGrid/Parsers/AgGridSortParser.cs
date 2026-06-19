using FlexQuery.NET.AgGrid.Models;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.AgGrid.Parsers;

public static class AgGridSortParser
{
    public static List<SortNode> Parse(IReadOnlyList<AgGridSortItem>? sortModel)
    {
        var result = new List<SortNode>();

        if (sortModel is null)
        {
            return result;
        }

        foreach (var sortItem in sortModel)
        {
            if (sortItem is null || string.IsNullOrWhiteSpace(sortItem.ColId))
            {
                continue;
            }

            result.Add(new SortNode
            {
                Field = sortItem.ColId,
                Descending = ParseDirection(sortItem.Sort, sortItem.ColId)
            });
        }

        return result;
    }

    private static bool ParseDirection(string? sort, string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return false;
        }

        return sort.Trim().ToLowerInvariant() switch
        {
            "asc" => false,
            "desc" => true,
            _ => throw new FormatException($"Unsupported AG Grid sort direction '{sort}' for field '{fieldPath}'.")
        };
    }
}
