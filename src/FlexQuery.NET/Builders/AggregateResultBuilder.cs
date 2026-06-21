using FlexQuery.NET.Models;

namespace FlexQuery.NET.Builders;

internal static class AggregateResultBuilder
{
    public static Dictionary<string, Dictionary<string, object>>? Build(
        object? aggregateRow,
        IReadOnlyCollection<AggregateModel> aggregates)
    {
        if (aggregateRow == null)
        {
            return null;
        }

        var aggregateLookup = aggregates.ToDictionary(
            a => a.Alias,
            StringComparer.OrdinalIgnoreCase);

        var grandTotals =
            new Dictionary<string, Dictionary<string, object>>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var prop in aggregateRow.GetType().GetProperties())
        {
            if (!aggregateLookup.TryGetValue(prop.Name, out var aggregate))
            {
                continue;
            }

            var fieldName = aggregate.Field ?? "all";

            if (!grandTotals.TryGetValue(fieldName, out var fnDict))
            {
                fnDict = new Dictionary<string, object>(
                    StringComparer.OrdinalIgnoreCase);

                grandTotals[fieldName] = fnDict;
            }

            fnDict[aggregate.Function] =
                prop.GetValue(aggregateRow) ?? 0;
        }

        return grandTotals.Count > 0
            ? grandTotals
            : null;
    }
}