using System.Security.Cryptography;
using System.Text;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Builders;

/// <summary>
/// Provides canonical AST normalization for filter trees.
///
/// Responsibilities:
/// - Deterministic ordering
/// - Operator normalization
/// - Field normalization
/// - Logical group flattening
/// - Structural fingerprint generation
/// - Cache-key support
///
/// This normalization layer ensures logically equivalent queries
/// produce identical cache keys.
/// </summary>
internal static class FilterNormalizer
{
    /// <summary>
    /// Fully normalizes a filter tree.
    /// </summary>
    public static FilterGroupNode? Normalize(FilterGroupNode? group)
    {
        return group is null
            ? null
            : NormalizeGroup(group);
    }

    /// <summary>
    /// Normalizes structural ordering while preserving original casing.
    /// Useful for UI display scenarios.
    /// </summary>
    public static FilterGroupNode? NormalizeOrder(FilterGroupNode? group)
    {
        return group is null
            ? null
            : NormalizeOrderGroup(group);
    }

    /// <summary>
    /// Generates a canonical cache key.
    /// </summary>
    public static string GenerateCacheKey(FilterGroupNode? group)
    {
        if (group is null)
            return string.Empty;

        var normalized = Normalize(group);

        return BuildSortKey(normalized);
    }

    /// <summary>
    /// Generates SHA256 fingerprint for distributed cache scenarios.
    /// </summary>
    public static string GenerateHash(FilterGroupNode? group)
    {
        var key = GenerateCacheKey(group);

        using var sha = SHA256.Create();

        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key));

        return Convert.ToHexString(bytes);
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
                    var normalizedCondition = NormalizeCondition(condition);

                    if (normalizedCondition is not null)
                    {
                        normalized.Children.Add(normalizedCondition);
                    }
                    break;
                }
                case FilterGroupNode childGroup:
                {
                    var normalizedChild = NormalizeGroup(childGroup);
                    if (normalizedChild.Children.Count == 0)
                        continue;

                    // Flatten redundant groups
                    if (!normalizedChild.IsNegated &&
                        normalizedChild.Logic == normalized.Logic)
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

        // Remove duplicate conditions
        normalized.Children = RemoveDuplicates(normalized.Children);
        // Deterministic ordering
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

                    if (!normalizedChild.IsNegated &&
                        normalizedChild.Logic == normalized.Logic)
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

    private static FilterConditionNode? NormalizeCondition(FilterConditionNode condition)
    {
        var field = condition.Field?.Trim();

        if (string.IsNullOrWhiteSpace(field))
            return null;

        return new FilterConditionNode
        {
            Field = field.ToLowerInvariant(),
            Operator = FilterOperators.Normalize(condition.Operator),
            Value = NormalizeValue(condition.Value),
            IsNegated = condition.IsNegated,
            ScopedFilter = Normalize(condition.ScopedFilter)
        };
    }

    private static string? NormalizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return value.Trim();
    }

    private static List<FilterNode> RemoveDuplicates(List<FilterNode> nodes)
    {
        var map = new Dictionary<string, FilterNode>(StringComparer.Ordinal);

        foreach (var node in nodes)
        {
            var key = BuildSortKey(node);

            map.TryAdd(key, node);
        }

        return map.Values.ToList();
    }

    private static int CompareFilterNodes(FilterNode x, FilterNode y)
    {
        return string.CompareOrdinal(BuildSortKey(x), BuildSortKey(y));
    }

    private static int CompareFilterNodesPreservingCase(FilterNode x, FilterNode y)
    {
        return string.CompareOrdinal(
            BuildSortKeyPreservingCase(x),
            BuildSortKeyPreservingCase(y));
    }

    private static string BuildSortKey(FilterNode? node)
    {
        if (node is null)
            return string.Empty;

        return node switch
        {
            FilterConditionNode condition =>
                "C|" +
                "F:" + condition.Field.Trim().ToLowerInvariant() +
                "|O:" + FilterOperators.Normalize(condition.Operator) +
                "|N:" + condition.IsNegated +
                "|V:" + EscapeKey(condition.Value) +
                "|S:" + BuildSortKey(condition.ScopedFilter),

            FilterGroupNode group =>
                "G|" +
                "L:" + group.Logic +
                "|N:" + group.IsNegated +
                "|Children:[" +
                string.Join(",", group.Children.Select(BuildSortKey)) +
                "]",

            _ => string.Empty
        };
    }

    private static string BuildSortKeyPreservingCase(FilterNode? node)
    {
        if (node is null)
            return string.Empty;

        return node switch
        {
            FilterConditionNode condition =>
                "C|" +
                "F:" + condition.Field.Trim() +
                "|O:" + condition.Operator +
                "|N:" + condition.IsNegated +
                "|V:" + EscapeKey(condition.Value) +
                "|S:" + BuildSortKeyPreservingCase(condition.ScopedFilter),

            FilterGroupNode group =>
                "G|" +
                "L:" + group.Logic +
                "|N:" + group.IsNegated +
                "|Children:[" +
                string.Join(",", group.Children.Select(BuildSortKeyPreservingCase)) +
                "]",

            _ => string.Empty
        };
    }

    private static string EscapeKey(string? segment)
    {
        return segment is null
            ? string.Empty
            : segment
                .Replace("\\", "\\\\")
                .Replace("|", "\\|")
                .Replace(",", "\\,")
                .Replace("[", "\\[")
                .Replace("]", "\\]");
    }
}