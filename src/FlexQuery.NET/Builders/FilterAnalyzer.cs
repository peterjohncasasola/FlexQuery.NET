using FlexQuery.NET.Models;

namespace FlexQuery.NET.Builders;

/// <summary>
/// Extracts (and re-bases) filter subtrees that target a specific navigation path.
/// Used to align projected child collections with the same filter semantics used
/// to filter parents (without altering the parent WHERE/EXISTS logic).
/// </summary>
internal static class FilterAnalyzer
{
    /// <summary>
    /// Extracts a filter subtree that applies to <paramref name="navigation"/>,
    /// and strips the <c>navigation.</c> prefix from all leaf condition fields.
    ///
    /// Safety rule:
    /// - AND groups: can partially extract matching conditions/groups (other branches ignored).
    /// - OR  groups: only extract when ALL branches apply to the navigation (otherwise null).
    /// </summary>
    public static FilterGroupNode? ExtractForNavigation(FilterGroupNode group, string navigation)
    {
        if (group.Children.Count == 0) return null;
        if (string.IsNullOrWhiteSpace(navigation)) return null;

        var prefix = navigation.Trim() + ".";

        var extracted = new FilterGroupNode { Logic = group.Logic };
        var hasNonApplicable = false;

        foreach (var child in group.Children)
        {
            if (child is FilterConditionNode f)
            {
                if (string.IsNullOrWhiteSpace(f.Field))
                {
                    hasNonApplicable = true;
                    continue;
                }

                if (f.Field.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var rebased = f.Field[prefix.Length..];
                    if (!string.IsNullOrWhiteSpace(rebased))
                    {
                        extracted.Children.Add(new FilterConditionNode
                        {
                            Field = rebased,
                            Operator = f.Operator,
                            Value = f.Value
                        });
                    }
                    else
                    {
                        hasNonApplicable = true;
                    }

                    continue;
                }

                hasNonApplicable = true;
            }
            else if (child is FilterGroupNode g)
            {
                var sub = ExtractForNavigation(g, navigation);
                if (sub is null)
                {
                    hasNonApplicable = true;
                    continue;
                }

                extracted.Children.Add(sub);
            }
        }

        if (!HasAnyCondition(extracted)) return null;

        // OR must be "fully applicable", otherwise the parent might match a different OR branch.
        if (group.Logic == LogicOperator.Or && hasNonApplicable)
        {
            return null;
        }

        return extracted;
    }

    /// <summary>
    /// Generates a stable cache key for a filter subtree.
    /// </summary>
    /// <param name="group">The filter group node to generate a key for.</param>
    /// <returns>A string representing the cache key for this filter subtree.</returns>
    public static string CacheKey(FilterGroupNode? group)
    {
        group = FilterNormalizer.Normalize(group);
        if (group is null) return string.Empty;

        var parts = group.Children.Select(child => child switch
        {
            FilterGroupNode g => CacheKey(g),
            FilterConditionNode f => Escape(f.Field) + ":" + Escape(f.Operator) + ":" + Escape(f.Value),
            _ => string.Empty
        });

        return "{" +
               "L=" + (group.Logic == LogicOperator.Or ? "or" : "and") +
               ";C=" + string.Join(",", parts) +
               "}";
    }

    private static bool HasAnyCondition(FilterGroupNode group)
        => group.Children.Any(c => c is FilterConditionNode || (c is FilterGroupNode g && HasAnyCondition(g)));

    private static string Escape(string? s)
        => s is null ? "∅" : s.Replace("\\", "\\\\").Replace(":", "\\:").Replace(",", "\\,").Replace("{", "\\{").Replace("}", "\\}");
}

