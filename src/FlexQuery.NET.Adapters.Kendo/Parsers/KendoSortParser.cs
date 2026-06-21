using FlexQuery.NET.Adapters.Kendo.Models;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Adapters.Kendo.Parsers;

/// <summary>
/// Parses Kendo UI sort descriptors into FlexQuery.NET sort nodes.
/// </summary>
internal static class KendoSortParser
{
    /// <summary>
    /// Parses a collection of Kendo UI sort descriptors into FlexQuery.NET sort nodes.
    /// </summary>
    /// <param name="sortModel">The collection of Kendo UI sort descriptors.</param>
    /// <returns>A list of FlexQuery.NET sort nodes.</returns>
    public static List<SortNode> Parse(IReadOnlyList<KendoSortDescriptor>? sortModel)
    {
        var result = new List<SortNode>();

        if (sortModel == null)
        {
            return result;
        }

        foreach (var sortItem in sortModel)
        {
            if (string.IsNullOrWhiteSpace(sortItem.Field))
            {
                continue;
            }

            result.Add(new SortNode
            {
                Field = sortItem.Field,
                Descending = ParseDirection(sortItem.Dir, sortItem.Field)
            });
        }

        return result;
    }

    /// <summary>
    /// Parses a Kendo UI sort direction string into a boolean indicating descending order.
    /// </summary>
    /// <param name="dir">The direction string ("asc" or "desc").</param>
    /// <param name="fieldPath">The field path for error messages.</param>
    /// <returns>True if descending, false if ascending.</returns>
    /// <exception cref="FormatException">Thrown when the direction is not supported.</exception>
    private static bool ParseDirection(string? dir, string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(dir))
        {
            return false;
        }

        return dir.Trim().ToLowerInvariant() switch
        {
            "asc" => false,
            "desc" => true,
            _ => throw new FormatException($"Unsupported Kendo sort direction '{dir}' for field '{fieldPath}'.")
        };
    }
}
