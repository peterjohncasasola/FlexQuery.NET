using System.Text.Json;
using System.Text.RegularExpressions;
using DynamicQueryable.Constants;
using DynamicQueryable.Models;
using DynamicQueryable.Parsers.Dsl;
using DynamicQueryable.Parsers.Jql;
using Microsoft.Extensions.Primitives;

namespace DynamicQueryable.Parsers;

/// <summary>
/// Parses query-string parameters from multiple formats into a unified
/// <see cref="QueryOptions"/> object.
///
/// Supported formats:
/// <list type="bullet">
///   <item>Generic  — filter[0].field / sort[0].field / page / pageSize / select</item>
///   <item>JSON     — filter={...json...}</item>
///   <item>DSL      — filter=name:eq:john</item>
///   <item>JQL      — query=name = "john"</item>
/// </list>
/// </summary>
public static class QueryOptionsParser
{
    private static readonly Regex SelectAggregatePattern = new(
        @"^(?<fn>sum|count|avg)\((?<field>[A-Za-z_][A-Za-z0-9_\.]*)?\)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex HavingPattern = new(
        @"^(?<fn>sum|count|avg)\((?<field>[A-Za-z_][A-Za-z0-9_\.]*)?\):(?<op>[A-Za-z_][A-Za-z0-9_]*):(?<value>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AggregateSortPattern = new(
        @"^(?<collection>[A-Za-z_][A-Za-z0-9_\.]*)\.(?<fn>sum|count|max|min|avg)\((?<field>[A-Za-z_][A-Za-z0-9_\.]*)?\)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ── Public entry point ───────────────────────────────────────────────

    /// <summary>
    /// Auto-detects the query-string format and parses into <see cref="QueryOptions"/>.
    /// Resilient: invalid or unrecognised keys are silently ignored.
    /// </summary>
    public static QueryOptions Parse(IEnumerable<KeyValuePair<string, StringValues>> queryString)
    {
        var dict = queryString
            .GroupBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Last().Value.ToString(),
                StringComparer.OrdinalIgnoreCase);

        if (dict.Count == 0) return new QueryOptions();

        if (dict.TryGetValue("query", out var jql) && !string.IsNullOrWhiteSpace(jql))
            return ParseJql(dict, jql);

        if (dict.Keys.Any(k => k.StartsWith("filter[0]", StringComparison.OrdinalIgnoreCase)))
            return ParseGeneric(dict);

        if (dict.TryGetValue("filter", out var filterVal) && !string.IsNullOrWhiteSpace(filterVal))
        {
            if (filterVal.TrimStart().StartsWith('{'))
                return ParseJsonFilter(dict);
            
            return ParseDslFilter(dict);
        }

        return ParseGeneric(dict);
    }

    // ── Generic Format ───────────────────────────────────────────────────
    //  ?filter[0].field=Name&filter[0].operator=contains&filter[0].value=john
    //  &sort[0].field=Age&sort[0].desc=true&page=1&pageSize=10&select=Name,Email
    //  &logic=and   (optional top-level logic)

    private static QueryOptions ParseGeneric(Dictionary<string, string> d)
    {
        var options = new QueryOptions();

        // Paging
        options.Paging.Page     = ParseInt(d, "page", 1);
        options.Paging.PageSize = ParseInt(d, "pageSize", 20);

        // Mode
        if (d.TryGetValue("mode", out var modeStr))
        {
            options.ProjectionMode = modeStr.Trim().ToLowerInvariant() switch
            {
                "flat"       => ProjectionMode.Flat,
                "flat-mixed" => ProjectionMode.FlatMixed,
                _            => ProjectionMode.Nested
            };
        }

        // Select
        if (d.TryGetValue("select", out var sel))
        {
            ParseSelectWithAggregates(options, sel);
        }

        if (d.TryGetValue("group", out var groupRaw))
            options.GroupBy = SplitCsv(groupRaw);

        if (d.TryGetValue("having", out var havingRaw))
            options.Having = ParseHaving(havingRaw);

        // Includes — parse both as plain strings (backward-compat) and as
        // structured IncludeNode trees that support inline JQL filters.
        if (d.TryGetValue("include", out var inc))
        {
            options.Includes         = SplitCsv(inc.Split('(')[0]); // plain names only
            options.FilteredIncludes = FilteredIncludeParser.Parse(inc);
        }

        // Top-level logic
        var logic = ParseLogic(d.GetValueOrDefault("logic", "and"));

        // Collect indexed filters: filter[0].field, filter[0].operator, filter[0].value
        var filterMap = CollectIndexed(d, "filter");
        var filters = new List<FilterCondition>();
        foreach (var (_, fields) in filterMap.OrderBy(x => x.Key))
        {
            var field = fields.GetValueOrDefault("field");
            if (string.IsNullOrWhiteSpace(field)) continue;
            filters.Add(new FilterCondition
            {
                Field    = field,
                Operator = FilterOperators.Normalize(fields.GetValueOrDefault("operator", "eq")),
                Value    = fields.GetValueOrDefault("value")
            });
        }

        if (filters.Count > 0)
            options.Filter = new FilterGroup { Logic = logic, Filters = filters };

        // Collect indexed sorts: sort[0].field, sort[0].desc
        var sortMap = CollectIndexed(d, "sort");
        foreach (var (_, fields) in sortMap.OrderBy(x => x.Key))
        {
            var field = fields.GetValueOrDefault("field");
            if (string.IsNullOrWhiteSpace(field)) continue;
            options.Sort.Add(new SortOption
            {
                Field      = field,
                Descending = ParseBool(fields.GetValueOrDefault("desc"))
            });
        }
        
        if (d.TryGetValue("sort", out var sortRaw))
            options.Sort.AddRange(ParseSort(sortRaw));

        return options;
    }

    private static void ParseSelectWithAggregates(QueryOptions options, string? rawSelect)
    {
        var fields = SplitCsv(rawSelect);
        if (fields.Count == 0)
        {
            options.Select = [];
            return;
        }

        var scalars = new List<string>();

        foreach (var field in fields)
        {
            var match = SelectAggregatePattern.Match(field);
            if (!match.Success)
            {
                scalars.Add(field);
                continue;
            }

            var fn = match.Groups["fn"].Value.ToLowerInvariant();
            var aggregateField = match.Groups["field"].Success
                ? match.Groups["field"].Value
                : null;

            options.Aggregates.Add(new AggregateModel
            {
                Function = fn,
                Field = string.IsNullOrWhiteSpace(aggregateField) ? null : aggregateField,
                Alias = BuildAggregateAlias(fn, aggregateField)
            });
        }

        options.Select = scalars;
    }

    private static HavingCondition? ParseHaving(string? rawHaving)
    {
        if (string.IsNullOrWhiteSpace(rawHaving)) return null;
        var match = HavingPattern.Match(rawHaving.Trim());
        if (!match.Success) return null;

        var fn = match.Groups["fn"].Value.ToLowerInvariant();
        var field = match.Groups["field"].Success ? match.Groups["field"].Value : null;

        return new HavingCondition
        {
            Function = fn,
            Field = string.IsNullOrWhiteSpace(field) ? null : field,
            Operator = FilterOperators.Normalize(match.Groups["op"].Value),
            Value = match.Groups["value"].Value
        };
    }

    internal static string BuildAggregateAlias(string function, string? field)
    {
        var normalized = string.IsNullOrWhiteSpace(field)
            ? "All"
            : field.Replace('.', '_');

        return $"{function.ToUpperInvariant()}_{normalized}";
    }

    // ── JSON Filter Format ───────────────────────────────────────────────
    //  ?filter={"logic":"and","filters":[{"field":"Name","operator":"contains","value":"john"}]}

    private static QueryOptions ParseJsonFilter(Dictionary<string, string> d)
    {
        var options = new QueryOptions();

        // Paging & select same as generic
        options.Paging.Page     = ParseInt(d, "page", 1);
        options.Paging.PageSize = ParseInt(d, "pageSize", 20);
        if (d.TryGetValue("select", out var sel)) options.Select = SplitCsv(sel);

        // Sort (generic format + sort string)
        options.Sort.AddRange(ParseGenericSorts(d));

        if (!d.TryGetValue("filter", out var json)) return options;

        try
        {
            using var doc = JsonDocument.Parse(json);
            
            if (doc.RootElement.TryGetProperty("select", out var selectEl))
            {
                options.SelectTree = Helpers.SelectTreeBuilder.ParseJsonSelect(selectEl);
            }

            if (doc.RootElement.TryGetProperty("filters", out _) || doc.RootElement.TryGetProperty("logic", out _))
            {
                options.Filter = ParseJsonGroup(doc.RootElement);
            }
            else if (doc.RootElement.TryGetProperty("filter", out var filterEl))
            {
                options.Filter = ParseJsonGroup(filterEl);
            }
        }
        catch { /* malformed JSON — ignore */ }

        return options;
    }

    private static FilterGroup ParseJsonGroup(JsonElement root)
    {
        var group = new FilterGroup();

        if (root.TryGetProperty("logic", out var logicEl))
            group.Logic = ParseLogic(logicEl.GetString());

        if (root.TryGetProperty("filters", out var filtersEl)
            && filtersEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in filtersEl.EnumerateArray())
            {
                // Nested group?
                if (item.TryGetProperty("logic", out _) || item.TryGetProperty("filters", out _))
                {
                    group.Groups.Add(ParseJsonGroup(item));
                    continue;
                }

                var field = item.TryGetProperty("field",    out var f) ? f.GetString() : null;
                var op    = item.TryGetProperty("operator", out var o) ? o.GetString() : "eq";
                var value = item.TryGetProperty("value",    out var v)
                    ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText())
                    : null;

                if (!string.IsNullOrWhiteSpace(field))
                    group.Filters.Add(new FilterCondition
                    {
                        Field    = field,
                        Operator = FilterOperators.Normalize(op),
                        Value    = value
                    });
            }
        }

        return group;
    }

    // DSL Filter Format
    //  ?filter=(name:eq:john|name:eq:doe)&age:gt:20

    private static QueryOptions ParseDslFilter(Dictionary<string, string> d)
    {
        var options = ParseGeneric(d);
        if (!d.TryGetValue("filter", out var filter)) return options;

        try
        {
            var ast = DslParser.Parse(filter);
            options.Filter = DslFilterConverter.ToFilterGroup(ast);
        }
        catch (DslParseException)
        {
            options.Filter = null;
        }

        return options;
    }

    // ── JQL-lite Filter Format ───────────────────────────────────────────
    //  ?query=(name = "john" OR name = "doe") AND age >= 20
    //
    // JQL parsing errors are NOT swallowed: invalid syntax should be surfaced to callers.
    private static QueryOptions ParseJql(Dictionary<string, string> d, string query)
    {
        var options = ParseGeneric(d);

        var ast = JqlParser.Parse(query);
        options.Filter = JqlFilterConverter.ToFilterGroup(ast);

        return options;
    }



    // ── Shared helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Collects keys like <c>prefix[index].subkey</c> into a nested dictionary
    /// indexed by the integer index, then sub-keyed by the sub-key name.
    /// </summary>
    private static SortedDictionary<int, Dictionary<string, string>> CollectIndexed(
        Dictionary<string, string> d, string prefix)
    {
        var result = new SortedDictionary<int, Dictionary<string, string>>();
        // matches: prefix[0].field  or  prefix[0][field]
        var regex = new Regex(
            $@"^{Regex.Escape(prefix)}\[(\d+)\][.\[]([^\]\s]+)\]?$",
            RegexOptions.IgnoreCase);

        foreach (var kv in d)
        {
            var m = regex.Match(kv.Key);
            if (!m.Success) continue;
            var idx    = int.Parse(m.Groups[1].Value);
            var subkey = m.Groups[2].Value.ToLowerInvariant();
            if (!result.TryGetValue(idx, out var inner))
                result[idx] = inner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            inner[subkey] = kv.Value;
        }

        return result;
    }



    private static int ParseInt(Dictionary<string, string> d, string key, int defaultValue)
        => d.TryGetValue(key, out var raw) && int.TryParse(raw, out var val) ? val : defaultValue;

    private static bool ParseBool(string? raw)
        => raw is not null && (raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1");

    private static LogicOperator ParseLogic(string? raw)
        => string.Equals(raw?.Trim(), "or", StringComparison.OrdinalIgnoreCase)
            ? LogicOperator.Or
            : LogicOperator.And;

    private static List<string> SplitCsv(string? raw)
        => string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .ToList();

    private static List<SortOption> ParseGenericSorts(Dictionary<string, string> d)
    {
        var result = new List<SortOption>();

        var sortMap = CollectIndexed(d, "sort");
        foreach (var (_, fields) in sortMap.OrderBy(x => x.Key))
        {
            var field = fields.GetValueOrDefault("field");
            if (string.IsNullOrWhiteSpace(field)) continue;
            result.Add(new SortOption
            {
                Field = field,
                Descending = ParseBool(fields.GetValueOrDefault("desc"))
            });
        }

        if (d.TryGetValue("sort", out var sortRaw))
            result.AddRange(ParseSort(sortRaw));

        return result;
    }

    internal static List<SortOption> ParseSort(string? sortRaw)
    {
        var result = new List<SortOption>();
        if (string.IsNullOrWhiteSpace(sortRaw)) return result;

        foreach (var item in sortRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(item)) continue;



            var parts = item.Split(':', 2, StringSplitOptions.TrimEntries);
            var field = parts[0];
            if (string.IsNullOrWhiteSpace(field)) continue;

            var direction = parts.Length > 1 ? parts[1] : "asc";
            var isDesc = direction.Equals("desc", StringComparison.OrdinalIgnoreCase);

            var aggregateMatch = AggregateSortPattern.Match(field);
            if (aggregateMatch.Success)
            {
                var aggregate = aggregateMatch.Groups["fn"].Value.ToLowerInvariant();
                var collection = aggregateMatch.Groups["collection"].Value;
                var aggregateField = aggregateMatch.Groups["field"].Success
                    ? aggregateMatch.Groups["field"].Value
                    : null;

                result.Add(new SortOption
                {
                    Field = collection,
                    Descending = isDesc,
                    Aggregate = aggregate,
                    AggregateField = string.IsNullOrWhiteSpace(aggregateField) ? null : aggregateField
                });
                continue;
            }

            result.Add(new SortOption
            {
                Field = field,
                Descending = isDesc
            });
        }

        return result;
    }
}
