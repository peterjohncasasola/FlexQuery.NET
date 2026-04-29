using System.Text.RegularExpressions;
using DynamicQueryable.Constants;
using DynamicQueryable.Models;

namespace DynamicQueryable.Parsers;

internal static class SpatieQueryParser
{
    public static QueryOptions Parse(Dictionary<string, string> d)
    {
        var options = new QueryOptions();

        options.Paging.Page = ParseInt(d, "page", 1);
        options.Paging.PageSize = ParseInt(d, "per_page", ParseInt(d, "pageSize", 20));

        options.Filter = ParseSpatieFilterGroup(d);

        if (d.TryGetValue("sort", out var sortRaw))
        {
            foreach (var part in sortRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var desc = part.StartsWith('-');
                var field = desc ? part[1..] : part;
                if (!string.IsNullOrWhiteSpace(field))
                {
                    options.Sort.Add(new SortOption { Field = field, Descending = desc });
                }
            }
        }

        var fieldsRegex = new Regex(@"^fields\[([^\]]+)\]$", RegexOptions.IgnoreCase);
        foreach (var kv in d)
        {
            var match = fieldsRegex.Match(kv.Key);
            if (!match.Success) continue;

            options.Select ??= new List<string>();
            var model = match.Groups[1].Value;
            var fields = SplitCsv(kv.Value);
            foreach (var f in fields)
            {
                options.Select.Add(f);
                options.Select.Add($"{model}.{f}");
            }
        }

        if (d.TryGetValue("include", out var includes))
        {
            options.Includes = SplitCsv(includes);
        }

        return options;
    }

    private sealed class SpatieFilterNode
    {
        public Dictionary<string, string> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);
        public SortedDictionary<int, SpatieFilterNode> AndChildren { get; } = new();
        public SortedDictionary<int, SpatieFilterNode> OrChildren { get; } = new();
    }

    private static FilterGroup? ParseSpatieFilterGroup(Dictionary<string, string> d)
    {
        var root = new SpatieFilterNode();

        foreach (var kv in d.Where(kv => kv.Key.StartsWith("filter[", StringComparison.OrdinalIgnoreCase)))
        {
            var tokens = ExtractBracketTokens(kv.Key);
            if (tokens.Count == 0) continue;
            InsertSpatieFilter(root, tokens, kv.Value);
        }

        return BuildSpatieGroup(root);
    }

    private static void InsertSpatieFilter(SpatieFilterNode node, List<string> tokens, string value)
    {
        if (tokens.Count == 1)
        {
            var field = tokens[0];
            if (!string.Equals(field, "and", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(field, "or", StringComparison.OrdinalIgnoreCase))
            {
                node.Fields[field] = value;
            }
            return;
        }

        if (!TryParseLogicToken(tokens[0], out var logic) || !int.TryParse(tokens[1], out var index))
        {
            return;
        }

        var target = logic == LogicOperator.And ? node.AndChildren : node.OrChildren;
        if (!target.TryGetValue(index, out var child))
        {
            child = new SpatieFilterNode();
            target[index] = child;
        }

        InsertSpatieFilter(child, tokens[2..], value);
    }

    private static FilterGroup? BuildSpatieGroup(SpatieFilterNode node)
    {
        var hasFields = node.Fields.Count > 0;
        var hasAndChildren = node.AndChildren.Count > 0;
        var hasOrChildren = node.OrChildren.Count > 0;

        if (!hasFields && !hasAndChildren && !hasOrChildren) return null;

        if (!hasFields && hasOrChildren && !hasAndChildren)
            return BuildGroupFromIndexedChildren(node.OrChildren, LogicOperator.Or);

        if (!hasFields && hasAndChildren && !hasOrChildren)
            return BuildGroupFromIndexedChildren(node.AndChildren, LogicOperator.And);

        var mixedGroup = new FilterGroup { Logic = LogicOperator.And };
        AddFieldFilters(mixedGroup, node.Fields);
        AddIndexedChildrenAsGroups(mixedGroup, node.AndChildren, LogicOperator.And);
        AddIndexedChildrenAsGroups(mixedGroup, node.OrChildren, LogicOperator.Or);
        return mixedGroup;
    }

    private static FilterGroup BuildGroupFromIndexedChildren(
        SortedDictionary<int, SpatieFilterNode> children,
        LogicOperator logic)
    {
        var group = new FilterGroup { Logic = logic };

        foreach (var child in children.OrderBy(c => c.Key).Select(c => c.Value))
        {
            if (TryBuildSingleFilter(child, out var filter))
            {
                group.Filters.Add(filter);
                continue;
            }

            var nested = BuildSpatieGroup(child);
            if (nested != null)
            {
                group.Groups.Add(nested);
            }
        }

        return group;
    }

    private static bool TryBuildSingleFilter(SpatieFilterNode node, out FilterCondition filter)
    {
        filter = null!;
        if (node.Fields.Count != 1 || node.AndChildren.Count > 0 || node.OrChildren.Count > 0)
        {
            return false;
        }

        var field = node.Fields.Keys.Single();
        filter = new FilterCondition
        {
            Field = field,
            Operator = FilterOperators.Equal,
            Value = node.Fields[field]
        };
        return true;
    }

    private static void AddFieldFilters(FilterGroup group, Dictionary<string, string> fields)
    {
        foreach (var field in fields)
        {
            group.Filters.Add(new FilterCondition
            {
                Field = field.Key,
                Operator = FilterOperators.Equal,
                Value = field.Value
            });
        }
    }

    private static void AddIndexedChildrenAsGroups(
        FilterGroup group,
        SortedDictionary<int, SpatieFilterNode> children,
        LogicOperator logic)
    {
        if (children.Count > 0)
        {
            group.Groups.Add(BuildGroupFromIndexedChildren(children, logic));
        }
    }

    private static bool TryParseLogicToken(string token, out LogicOperator logic)
    {
        if (string.Equals(token, "or", StringComparison.OrdinalIgnoreCase))
        {
            logic = LogicOperator.Or;
            return true;
        }

        if (string.Equals(token, "and", StringComparison.OrdinalIgnoreCase))
        {
            logic = LogicOperator.And;
            return true;
        }

        logic = LogicOperator.And;
        return false;
    }

    private static List<string> ExtractBracketTokens(string key)
    {
        var matches = Regex.Matches(key, @"\[([^\]]+)\]");
        if (matches.Count == 0) return [];

        return matches
            .Select(m => m.Groups[1].Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();
    }

    private static int ParseInt(Dictionary<string, string> d, string key, int defaultValue)
        => d.TryGetValue(key, out var raw) && int.TryParse(raw, out var val) ? val : defaultValue;

    private static List<string> SplitCsv(string? raw)
        => string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .ToList();
}
