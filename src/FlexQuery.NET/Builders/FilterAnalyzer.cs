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
    public static FilterGroup? ExtractForNavigation(FilterGroup group, string navigation)
    {
        if (group.Filters.Count == 0 && group.Groups.Count == 0) return null;
        if (string.IsNullOrWhiteSpace(navigation)) return null;

        var prefix = navigation.Trim() + ".";

        var extracted = new FilterGroup { Logic = group.Logic };
        var hasNonApplicable = false;

        foreach (var f in group.Filters)
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
                    extracted.Filters.Add(new FilterCondition
                    {
                        Field = rebased,
                        Operator = f.Operator,
                        Value = f.Value
                    });
                }
                else
                {
                    // Filter is directly on the navigation (rare/unsupported) → treat as non-applicable.
                    hasNonApplicable = true;
                }

                continue;
            }

            hasNonApplicable = true;
        }

        foreach (var g in group.Groups)
        {
            var sub = ExtractForNavigation(g, navigation);
            if (sub is null)
            {
                hasNonApplicable = true;
                continue;
            }

            extracted.Groups.Add(sub);
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
    public static string CacheKey(FilterGroup? group)
    {
        if (group is null) return string.Empty;

        return "{" +
               "L=" + (group.Logic == LogicOperator.Or ? "or" : "and") +
               ";F=" + string.Join(",", group.Filters.Select(f => Escape(f.Field) + ":" + Escape(f.Operator) + ":" + Escape(f.Value))) +
               ";G=" + string.Join(",", group.Groups.Select(CacheKey)) +
               "}";
    }

    private static bool HasAnyCondition(FilterGroup group)
        => group.Filters.Count > 0 || group.Groups.Any(HasAnyCondition);

    private static string Escape(string? s)
        => s is null ? "∅" : s.Replace("\\", "\\\\").Replace(":", "\\:").Replace(",", "\\,").Replace("{", "\\{").Replace("}", "\\}");
}

