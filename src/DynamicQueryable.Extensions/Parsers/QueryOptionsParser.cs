using System.Text.Json;
using System.Text.RegularExpressions;
using DynamicQueryable.Constants;
using DynamicQueryable.Models;
using DynamicQueryable.Parsers.Dsl;
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
///   <item>Syncfusion — where[0][field] / sorted[0][name] / skip / take</item>
///   <item>Laravel Spatie — filter[field]=value / sort=-field / fields[model]=a,b</item>
/// </list>
/// </summary>
public static class QueryOptionsParser
{
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

        // Detect format by key signatures
        if (IsDslFilterFormat(dict))   return ParseDslFilter(dict);
        if (IsJsonFilterFormat(dict))  return ParseJsonFilter(dict);
        if (IsSyncfusionFormat(dict))  return ParseSyncfusion(dict);
        if (IsSpatieFormat(dict))      return ParseSpatie(dict);
        return ParseGeneric(dict);
    }

    // ── Format detection ─────────────────────────────────────────────────

    private static bool IsSyncfusionFormat(Dictionary<string, string> d)
        => d.Keys.Any(k => k.StartsWith("where[", StringComparison.OrdinalIgnoreCase)
                        || k.StartsWith("sorted[", StringComparison.OrdinalIgnoreCase)
                        || d.ContainsKey("skip") || d.ContainsKey("take"));

    private static bool IsSpatieFormat(Dictionary<string, string> d)
        => d.Keys.Any(k =>
            k.StartsWith("filter[", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(k, @"^fields\[", RegexOptions.IgnoreCase))
        && !d.Keys.Any(k => Regex.IsMatch(k, @"^filter\[\d+\]", RegexOptions.IgnoreCase));

    private static bool IsJsonFilterFormat(Dictionary<string, string> d)
        => d.TryGetValue("filter", out var v) && v.TrimStart().StartsWith('{');

    private static bool IsDslFilterFormat(Dictionary<string, string> d)
        => d.TryGetValue("filter", out var v) && HasDslSyntax(v);

    private static bool HasDslSyntax(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var value = raw.TrimStart();
        if (value.StartsWith('{')) return false;

        return Regex.IsMatch(
            value,
            @"(^|[(&|])\s*[A-Za-z_][A-Za-z0-9_.]*\s*:\s*(eq|neq|gt|gte|lt|lte|contains|startswith|endswith|in|notin|between|isnull|notnull)(\s*:|\s*($|[)&|]))",
            RegexOptions.IgnoreCase);
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

        // Select
        if (d.TryGetValue("select", out var sel))
        {
            options.Select = SplitCsv(sel);
        }

        // Includes
        if (d.TryGetValue("include", out var inc))
        {
            options.Includes = SplitCsv(inc);
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

        return options;
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

        // Sort (generic format reused)
        var sortMap = CollectIndexed(d, "sort");
        foreach (var (_, fields) in sortMap.OrderBy(x => x.Key))
        {
            var field = fields.GetValueOrDefault("field");
            if (!string.IsNullOrWhiteSpace(field))
                options.Sort.Add(new SortOption
                {
                    Field      = field,
                    Descending = ParseBool(fields.GetValueOrDefault("desc"))
                });
        }

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

    // ── Syncfusion Format ────────────────────────────────────────────────
    //  ?where[0][field]=Name&where[0][operator]=contains&where[0][value]=john
    //  &sorted[0][name]=Age&sorted[0][direction]=descending
    //  &skip=0&take=10

    private static QueryOptions ParseSyncfusion(Dictionary<string, string> d)
    {
        var options = new QueryOptions();

        // Paging via skip/take
        var skip     = ParseInt(d, "skip", 0);
        var take     = ParseInt(d, "take", 20);
        var pageSize = take < 1 ? 20 : take;
        options.Paging.PageSize = pageSize;
        options.Paging.Page     = pageSize > 0 ? (skip / pageSize) + 1 : 1;

        // Select
        if (d.TryGetValue("select", out var sel)) options.Select = SplitCsv(sel);

        // Filters: where[0][field], where[0][operator], where[0][value]
        //          also supports where[0][condition] for AND/OR within index
        var whereMap = CollectBracketIndexed(d, "where");
        var filters  = new List<FilterCondition>();

        foreach (var (_, fields) in whereMap.OrderBy(x => x.Key))
        {
            var field = fields.GetValueOrDefault("field");
            if (string.IsNullOrWhiteSpace(field)) continue;

            // Syncfusion may embed predicateType inside the item for nested OR
            var op    = fields.GetValueOrDefault("operator") ?? "equal";
            var value = fields.GetValueOrDefault("value");

            filters.Add(new FilterCondition
            {
                Field    = field,
                Operator = NormalizeSyncfusionOperator(op),
                Value    = value
            });
        }

        // Syncfusion also uses a "isComplex" / "condition" at top level
        var topCondition = d.GetValueOrDefault("condition", "and");
        if (filters.Count > 0)
            options.Filter = new FilterGroup
            {
                Logic   = ParseLogic(topCondition),
                Filters = filters
            };

        // Sorts: sorted[0][name], sorted[0][direction]
        var sortedMap = CollectBracketIndexed(d, "sorted");
        foreach (var (_, fields) in sortedMap.OrderBy(x => x.Key))
        {
            var name = fields.GetValueOrDefault("name");
            if (string.IsNullOrWhiteSpace(name)) continue;
            var dir = fields.GetValueOrDefault("direction", "ascending");
            options.Sort.Add(new SortOption
            {
                Field      = name,
                Descending = dir.Contains("desc", StringComparison.OrdinalIgnoreCase)
            });
        }

        return options;
    }

    /// <summary>Maps Syncfusion operator names to canonical operators.</summary>
    private static string NormalizeSyncfusionOperator(string op) =>
        op.ToLowerInvariant() switch
        {
            "equal"              => FilterOperators.Equal,
            "notequal"           => FilterOperators.NotEqual,
            "greaterthan"        => FilterOperators.GreaterThan,
            "greaterthanorequal" => FilterOperators.GreaterThanOrEq,
            "lessthan"           => FilterOperators.LessThan,
            "lessthanorequal"    => FilterOperators.LessThanOrEq,
            "contains"           => FilterOperators.Contains,
            "startswith"         => FilterOperators.StartsWith,
            "endswith"           => FilterOperators.EndsWith,
            "isnull"             => FilterOperators.IsNull,
            "isnotnull"          => FilterOperators.IsNotNull,
            _                    => FilterOperators.Normalize(op)
        };

    // ── Laravel Spatie Format ────────────────────────────────────────────
    //  ?filter[name]=john&filter[age]=25&sort=-created_at
    //  &include=roles,permissions&fields[users]=name,email

    private static QueryOptions ParseSpatie(Dictionary<string, string> d)
        => SpatieQueryParser.Parse(d);

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

    /// <summary>
    /// Collects keys like <c>prefix[0][subkey]</c> (Syncfusion style double-bracket).
    /// </summary>
    private static SortedDictionary<int, Dictionary<string, string>> CollectBracketIndexed(
        Dictionary<string, string> d, string prefix)
    {
        var result = new SortedDictionary<int, Dictionary<string, string>>();
        var regex = new Regex(
            $@"^{Regex.Escape(prefix)}\[(\d+)\]\[([^\]]+)\]$",
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
}
