using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Constants;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Parses filter expressions in generic indexed format.
/// Format: filter[n].field=Name&amp;filter[n].operator=eq&amp;filter[n].value=value
/// </summary>
internal static class FilterParser
{
    /// <summary>
    /// Parses indexed filters from a dictionary into a <see cref="FilterGroupNode"/>.
    /// </summary>
    public static FilterGroupNode? Parse(IDictionary<string, string> d)
    {
        var filterMap = ParserUtilities.CollectIndexed(d, QueryOptionKeys.Filter);
        if (filterMap.Count == 0) return null;

        var children = new List<FilterNode>();
        var logic = ParserUtilities.ParseLogic(d.TryGetValue(QueryOptionKeys.Logic, out var l) ? l : null);

        foreach (var (_, fields) in filterMap.OrderBy(x => x.Key))
        {
            var field = fields.GetValueOrDefault(QueryOptionKeys.Field);
            if (string.IsNullOrWhiteSpace(field)) continue;

            children.Add(new FilterConditionNode
            {
                Field = field,
                Operator = FilterOperators.Normalize(fields.GetValueOrDefault(QueryOptionKeys.Operator, "eq")),
                Value = fields.GetValueOrDefault(QueryOptionKeys.Value)
            });
        }

        return children.Count > 0 
            ? new FilterGroupNode { Logic = logic, Children = children } 
            : null;
    }
}