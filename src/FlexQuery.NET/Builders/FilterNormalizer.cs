using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Builders;

/// <summary>
/// Normalizes filter ASTs into a deterministic, canonical form.
/// This is used before cache-key generation so equivalent queries map to the same cache entry.
/// </summary>
internal static class FilterNormalizer
{
    /// <summary>
    /// Normalizes a filter group node into a canonical form.
    /// This deterministically orders conditions and groups, lowercases field names,
    /// and normalizes operators for consistent cache-key generation.
    /// </summary>
    /// <param name="group">The filter group node to normalize.</param>
    /// <returns>A normalized filter group node, or null if the input is null.</returns>
    public static FilterGroupNode? Normalize(FilterGroupNode? group)
    {
        return group is null ? null : NormalizeGroup(group);
    }

    /// <summary>
    /// Normalizes a filter group node while preserving the original field name casing.
    /// Only the structural order and group logic are normalized; field names and operators
    /// retain their original case for display purposes.
    /// </summary>
    /// <param name="group">The filter group node to normalize.</param>
    /// <returns>A structurally normalized filter group node, or null if the input is null.</returns>
    public static FilterGroupNode? NormalizeOrder(FilterGroupNode? group)
    {
        return group is null ? null : NormalizeOrderGroup(group);
    }

    private static FilterGroupNode NormalizeGroup(FilterGroupNode group)
    {
        var normalized = new FilterGroupNode
        {
            Logic = group.Logic,
            IsNegated = group.IsNegated
        };

        foreach (var child in group.Children)
        {
            switch (child)
            {
                case FilterConditionNode condition:
                {
                    var field = condition.Field?.Trim();
                    if (string.IsNullOrWhiteSpace(field))
                        continue;

                    normalized.Children.Add(new FilterConditionNode
                    {
                        Field = field.ToLowerInvariant(),
                        Operator = FilterOperators.Normalize(condition.Operator),
                        Value = condition.Value,
                        IsNegated = condition.IsNegated,
                        ScopedFilter = Normalize(condition.ScopedFilter)
                    });
                    break;
                }
                case FilterGroupNode childGroup:
                {
                    var normalizedChild = NormalizeGroup(childGroup);
                    if (normalizedChild.Children.Count == 0)
                        continue;

                    if (!normalizedChild.IsNegated && normalizedChild.Logic == normalized.Logic)
                    {
                        normalized.Children.AddRange(normalizedChild.Children);
                    }
                    else
                    {
                        normalized.Children.Add(normalizedChild);
                    }

                    break;
                }
            }
        }

        normalized.Children.Sort(CompareFilterNodes);
        return normalized;
    }

    private static FilterGroupNode NormalizeOrderGroup(FilterGroupNode group)
    {
        var normalized = new FilterGroupNode
        {
            Logic = group.Logic,
            IsNegated = group.IsNegated
        };

        foreach (var child in group.Children)
        {
            switch (child)
            {
                case FilterConditionNode condition:
                {
                    var field = condition.Field?.Trim();
                    if (string.IsNullOrWhiteSpace(field))
                        continue;

                    normalized.Children.Add(new FilterConditionNode
                    {
                        Field = field,
                        Operator = condition.Operator,
                        Value = condition.Value,
                        IsNegated = condition.IsNegated,
                        ScopedFilter = NormalizeOrder(condition.ScopedFilter)
                    });
                    break;
                }
                case FilterGroupNode childGroup:
                {
                    var normalizedChild = NormalizeOrderGroup(childGroup);
                    if (normalizedChild.Children.Count == 0)
                        continue;

                    if (!normalizedChild.IsNegated && normalizedChild.Logic == normalized.Logic)
                    {
                        normalized.Children.AddRange(normalizedChild.Children);
                    }
                    else
                    {
                        normalized.Children.Add(normalizedChild);
                    }

                    break;
                }
            }
        }

        normalized.Children.Sort(CompareFilterNodesPreservingCase);
        return normalized;
    }

    private static int CompareFilterNodes(FilterNode x, FilterNode y)
    {
        return string.CompareOrdinal(BuildSortKey(x), BuildSortKey(y));
    }

    private static int CompareFilterNodesPreservingCase(FilterNode x, FilterNode y)
    {
        return string.CompareOrdinal(BuildSortKeyPreservingCase(x), BuildSortKeyPreservingCase(y));
    }

    private static string BuildSortKeyPreservingCase(FilterNode? node)
    {
        if (node is null)
            return string.Empty;

        return node switch
        {
            FilterConditionNode condition =>
                "C|F:" + condition.Field.Trim() +
                "|O:" + condition.Operator +
                "|N:" + condition.IsNegated +
                "|V:" + EscapeKey(condition.Value) +
                "|S:" + BuildSortKeyPreservingCase(condition.ScopedFilter),

            FilterGroupNode group =>
                "G|L:" + group.Logic +
                "|N:" + group.IsNegated +
                "|Children:[" + string.Join(",", group.Children.Select(BuildSortKeyPreservingCase)) + "]",

            _ => string.Empty
        };
    }

    private static string BuildSortKey(FilterNode? node)
    {
        if (node is null)
            return string.Empty;

        return node switch
        {
            FilterConditionNode condition =>
                "C|F:" + condition.Field.Trim().ToLowerInvariant() +
                "|O:" + FilterOperators.Normalize(condition.Operator) +
                "|N:" + condition.IsNegated +
                "|V:" + EscapeKey(condition.Value) +
                "|S:" + BuildSortKey(condition.ScopedFilter),

            FilterGroupNode group =>
                "G|L:" + group.Logic +
                "|N:" + group.IsNegated +
                "|Children:[" + string.Join(",", group.Children.Select(BuildSortKey)) + "]",

            _ => string.Empty
        };
    }

    private static string EscapeKey(string? segment)
        => segment is null ? string.Empty : segment.Replace("\\", "\\\\").Replace("|", "\\|").Replace(",", "\\,").Replace("[", "\\[").Replace("]", "\\]");
}
