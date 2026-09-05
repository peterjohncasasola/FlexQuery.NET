using FlexQuery.NET.Models;

namespace FlexQuery.NET.Mapping;

internal static class RequestedNavigationGraph
{
    public static IReadOnlySet<string> Collect(QueryOptions queryOptions)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectSegments(queryOptions.Includes, paths);
        CollectSegments(queryOptions.Expand?.Select(e => e.Path), paths);
        return paths;
    }

    private static void CollectSegments(IEnumerable<string?>? source, ISet<string> target)
    {
        ArgumentNullException.ThrowIfNull(target);
        
        if (source is null)
            return;

        foreach (var path in source)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var current = "";
            foreach (var segment in segments)
            {
                current = current.Length == 0 ? segment : $"{current}.{segment}";
                target.Add(current);
            }
        }
    }
}
