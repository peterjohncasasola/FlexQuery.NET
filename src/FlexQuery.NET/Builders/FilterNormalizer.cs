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

        // Deterministic ordering and duplicate removal in one pass
        normalized.Children = DeduplicateAndSort(normalized.Children, preserveCase: false);
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

        normalized.Children = DeduplicateAndSort(normalized.Children, preserveCase: true);
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

    private sealed record KeyedNode(string Key, FilterNode Node);

    private static List<FilterNode> DeduplicateAndSort(List<FilterNode> nodes, bool preserveCase)
    {
        if (nodes.Count == 0) return nodes;

        var map = new Dictionary<string, FilterNode>(nodes.Count, StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            var key = preserveCase ? BuildSortKeyPreservingCase(node) : BuildSortKey(node);
            map.TryAdd(key, node);
        }

        var keyed = new List<KeyedNode>(map.Count);
        foreach (var kv in map)
            keyed.Add(new KeyedNode(kv.Key, kv.Value));

        keyed.Sort(static (a, b) => string.CompareOrdinal(a.Key, b.Key));

        var result = new List<FilterNode>(keyed.Count);
        foreach (var item in keyed)
            result.Add(item.Node);

        return result;
    }

    private static string BuildSortKey(FilterNode? node)
    {
        if (node is null) return string.Empty;
        var sb = new StringBuilder();
        BuildSortKeyInternal(node, sb, false);
        return sb.ToString();
    }

    private static string BuildSortKeyPreservingCase(FilterNode? node)
    {
        if (node is null) return string.Empty;
        var sb = new StringBuilder();
        BuildSortKeyInternal(node, sb, true);
        return sb.ToString();
    }

    private static void BuildSortKeyInternal(FilterNode? node, StringBuilder sb, bool preserveCase)
    {
        if (node is null) return;

        switch (node)
        {
            case FilterConditionNode condition:
                sb.Append("C|F:");
                sb.Append(preserveCase ? condition.Field?.Trim() : condition.Field?.Trim().ToLowerInvariant());
                sb.Append("|O:");
                sb.Append(preserveCase ? condition.Operator : FilterOperators.Normalize(condition.Operator));
                sb.Append("|N:");
                sb.Append(condition.IsNegated);
                sb.Append("|V:");
                EscapeKeyInternal(condition.Value, sb);
                sb.Append("|S:");
                BuildSortKeyInternal(condition.ScopedFilter, sb, preserveCase);
                break;

            case FilterGroupNode group:
                sb.Append("G|L:");
                sb.Append(group.Logic);
                sb.Append("|N:");
                sb.Append(group.IsNegated);
                sb.Append("|Children:[");
                for (int i = 0; i < group.Children.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    BuildSortKeyInternal(group.Children[i], sb, preserveCase);
                }
                sb.Append(']');
                break;
        }
    }

    private static void EscapeKeyInternal(string? segment, StringBuilder sb)
    {
        if (string.IsNullOrEmpty(segment)) return;
        
        for (int i = 0; i < segment.Length; i++)
        {
            var c = segment[i];
            if (c is '\\' or '|' or ',' or '[' or ']')
            {
                sb.Append('\\');
            }
            sb.Append(c);
        }
    }
}