using FlexQuery.NET.Caching;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers.Dsl;
using FlexQuery.NET.Parsers.Jql;
using Microsoft.Extensions.Primitives;
using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Parses query-string parameters from multiple formats into a unified
/// <see cref="QueryOptions"/> object.
///
/// Supported formats:
/// <list type="bullet">
///   <item>Generic  — filter[0].field / sort[0].field / page / pageSize / select</item>
///   <item>JSON     — filter={...json...}</item>
///   <item>DSL      — filter=name:eq:john (Primary Syntax)</item>
///   <item>OData    — $filter=name eq 'john' (Compatibility Syntax)</item>
///   <item>JQL      — query=name = "john" (Legacy/Deprecated Syntax)</item>
/// </list>
/// </summary>
public static class QueryOptionsParser
{
    private static readonly Regex SelectAggregatePattern = new(
        @"^(?:(?<fn>sum|count|avg)\((?<field>[A-Za-z_][A-Za-z0-9_\.]*)?\)|(?<field2>[A-Za-z_][A-Za-z0-9_\.]*)\.(?<fn2>sum|count|avg)\(\))$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex HavingPattern = new(
        @"^(?<fn>sum|count|avg)(?:\((?<field>[A-Za-z_][A-Za-z0-9_\.]*)?\)|:(?<field2>[A-Za-z_][A-Za-z0-9_\.]*)):(?<op>[A-Za-z_][A-Za-z0-9_]*):(?<value>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AggregateSortPattern = new(
        @"^(?<collection>[A-Za-z_][A-Za-z0-9_\.]*)\.(?<fn>sum|count|max|min|avg)\((?<field>[A-Za-z_][A-Za-z0-9_\.]*)?\)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly List<IQueryParser> _parsers = new() 
    { 
        new JqlParser(),
        new FlexQueryDslParser() 
    };

    static QueryOptionsParser()
    {
        try
        {
            // Try to dynamically discover and register MiniODataParser if its assembly is present in the AppDomain
            var odataParserType = Type.GetType("FlexQuery.NET.MiniOData.Parsers.MiniODataParser, FlexQuery.NET.MiniOData");
            if (odataParserType != null)
            {
                var parser = (IQueryParser)Activator.CreateInstance(odataParserType)!;
                RegisterParser(parser);
            }
        }
        catch
        {
            // Ignore if the assembly is not loaded or available
        }
    }

    /// <summary>
    /// Registers a new query parser implementation.
    /// New parsers are given priority over existing ones.
    /// </summary>
    /// <param name="parser">The parser to register.</param>
    public static void RegisterParser(IQueryParser parser) => _parsers.Insert(0, parser);

    // ── Public entry point ───────────────────────────────────────────────

    /// <summary>
    /// Parses a strongly typed <see cref="QueryRequest"/> into <see cref="QueryOptions"/>.
    /// </summary>
    [Obsolete("Use Parse(FlexQueryParameters) instead for better separation of concerns and flexibility.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static QueryOptions Parse(QueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        if (!string.IsNullOrWhiteSpace(request.Query)) dict["query"] = request.Query;
        if (!string.IsNullOrWhiteSpace(request.Filter)) dict["filter"] = request.Filter;
        if (!string.IsNullOrWhiteSpace(request.Sort)) dict["sort"] = request.Sort;
        if (!string.IsNullOrWhiteSpace(request.Select)) dict["select"] = request.Select;
        if (!string.IsNullOrWhiteSpace(request.Include)) dict["include"] = request.Include;
        if (!string.IsNullOrWhiteSpace(request.GroupBy)) dict["group"] = request.GroupBy;
        if (!string.IsNullOrWhiteSpace(request.Having)) dict["having"] = request.Having;
        if (!string.IsNullOrWhiteSpace(request.Mode)) dict["mode"] = request.Mode;
        
        if (request.Page.HasValue) dict["page"] = request.Page.Value.ToString();
        if (request.PageSize.HasValue) dict["pageSize"] = request.PageSize.Value.ToString();
        if (request.IncludeCount.HasValue) dict["includeCount"] = request.IncludeCount.Value.ToString();
        if (request.Distinct.HasValue) dict["distinct"] = request.Distinct.Value.ToString();

        var parameters = new FlexQueryParameters
        {
            Query = request.Query,
            Filter = request.Filter,
            Sort = request.Sort,
            Select = request.Select,
            Include = request.Include,
            GroupBy = request.GroupBy,
            Having = request.Having,
            Page = request.Page,
            PageSize = request.PageSize,
            IncludeCount = request.IncludeCount,
            Distinct = request.Distinct,
            Mode = request.Mode,
            RawParameters = dict
        };

        return Parse(parameters);
    }
    /// <summary>
    /// Parses a strongly typed <see cref="FlexQueryParameters"/> into <see cref="QueryOptions"/>.
    /// </summary>
    /// <param name="parameters">The raw query parameters from the client.</param>
    /// <param name="syntax">The expected query syntax. Defaults to <see cref="QuerySyntax.AutoDetect"/>.</param>
    /// <returns>A unified <see cref="QueryOptions"/> object.</returns>
    public static QueryOptions Parse(FlexQueryParameters parameters, QuerySyntax syntax = QuerySyntax.AutoDetect)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        // Try Cache first (cache key includes syntax for safety)
        string? rawKey = null;
        if (parameters.RawParameters != null && parameters.RawParameters.Count > 0)
        {
            rawKey = string.Join("&", parameters.RawParameters
                .OrderBy(x => x.Key)
                .Select(x => $"{x.Key}={x.Value}"));
        }

        var cacheKey = new ParsedQueryCacheKey(
            parameters.Query, parameters.Filter, parameters.Sort, parameters.Select,
            parameters.Include, parameters.GroupBy, parameters.Having,
            parameters.Page, parameters.PageSize, parameters.IncludeCount,
            parameters.Distinct, parameters.Mode, rawKey, syntax.ToString());

        if (ParserCache.TryGet(cacheKey, out var cached))
        {
            return cached!;
        }

        IQueryParser? parser = null;

        if (syntax != QuerySyntax.AutoDetect)
        {
            parser = _parsers.FirstOrDefault(p => p.Syntax == syntax);
        }

        if (parser == null)
        {
            // Auto-detect or fallback
            parser = _parsers.FirstOrDefault(p => p.CanParse(parameters)) ?? _parsers.Last();
        }

        var options = parser.Parse(parameters);

        // Store in Cache
        ParserCache.Set(cacheKey, options);

        return options;
    }

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

        var parameters = new FlexQueryParameters
        {
            Query = dict.GetValueOrDefault("query"),
            Filter = dict.GetValueOrDefault("filter") ?? dict.GetValueOrDefault("$filter"),
            Sort = dict.GetValueOrDefault("sort") ?? dict.GetValueOrDefault("orderby") ?? dict.GetValueOrDefault("$orderby"),
            Select = dict.GetValueOrDefault("select") ?? dict.GetValueOrDefault("$select"),
            Include = dict.GetValueOrDefault("include") ?? dict.GetValueOrDefault("expand") ?? dict.GetValueOrDefault("$expand"),
            GroupBy = dict.GetValueOrDefault("group"),
            Having = dict.GetValueOrDefault("having"),
            Page = dict.TryGetValue("page", out var p) && int.TryParse(p, out var page) ? page : null,
            PageSize = dict.TryGetValue("pageSize", out var ps) && int.TryParse(ps, out var pageSize) ? pageSize : null,
            IncludeCount = dict.TryGetValue("includeCount", out var ic) ? ic.Equals("true", StringComparison.OrdinalIgnoreCase) : null,
            Distinct = dict.TryGetValue("distinct", out var d) ? d.Equals("true", StringComparison.OrdinalIgnoreCase) : null,
            Mode = dict.GetValueOrDefault("mode"),
            RawParameters = dict
        };

        return Parse(parameters);
    }

    internal static QueryOptions InternalParseDictionary(IDictionary<string, string> dict)
    {
        if (dict.Count == 0) return new QueryOptions();

        if (dict.TryGetValue("query", out var jql) && !string.IsNullOrWhiteSpace(jql))
            return InternalParseJql(dict, jql);

        if (dict.Keys.Any(k => k.StartsWith("filter[0]", StringComparison.OrdinalIgnoreCase)))
            return InternalParseGeneric(dict);

        if (dict.TryGetValue("filter", out var filterVal) && !string.IsNullOrWhiteSpace(filterVal))
        {
            if (filterVal.TrimStart().StartsWith('{'))
                return InternalParseJsonFilter(dict);
            
            return InternalParseDslFilter(dict);
        }

        return InternalParseGeneric(dict);
    }

    internal static LogicOperator InternalParseLogic(string? raw)
        => string.Equals(raw?.Trim(), "or", StringComparison.OrdinalIgnoreCase)
            ? LogicOperator.Or
            : LogicOperator.And;

    internal static SortedDictionary<int, Dictionary<string, string>> InternalCollectIndexed(
        IDictionary<string, string> d, string prefix)
    {
        var result = new SortedDictionary<int, Dictionary<string, string>>();
        var prefixSpan = prefix.AsSpan();

        foreach (var kv in d)
        {
            if (TryParseIndexedKey(kv.Key.AsSpan(), prefixSpan, out var idx, out var subkey))
            {
                if (!result.TryGetValue(idx, out var inner))
                    result[idx] = inner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                inner[subkey] = kv.Value;
            }
        }

        return result;
    }

    // ── Generic Format ───────────────────────────────────────────────────
    //  ?filter[0].field=Name&filter[0].operator=contains&filter[0].value=john
    //  &sort[0].field=Age&sort[0].desc=true&page=1&pageSize=10&select=Name,Email
    //  &logic=and   (optional top-level logic)

    internal static QueryOptions InternalParseGeneric(IDictionary<string, string> d)
    {
        var options = new QueryOptions();

        // Paging
        options.Paging.Page     = InternalParseInt(d, "page", 1);
        options.Paging.PageSize = InternalParseInt(d, "pageSize", 20);

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
            InternalParseSelectWithAggregates(options, sel);
        }

        if (d.TryGetValue("group", out var groupRaw))
            options.GroupBy = InternalSplitCsv(groupRaw);

        if (d.TryGetValue("having", out var havingRaw))
            options.Having = InternalParseHaving(havingRaw);

        // Includes — parse both as plain strings (backward-compat) and as
        // structured IncludeNode trees that support inline JQL filters.
        if (d.TryGetValue("include", out var inc))
        {
            options.Includes         = InternalSplitCsv(inc.Split('(')[0]); // plain names only
            options.FilteredIncludes = FilteredIncludeParser.Parse(inc);
        }

        // Top-level logic
        var logicValue = d.TryGetValue("logic", out var l) ? l : "and";
        var logic = InternalParseLogic(logicValue);

        // Collect indexed filters: filter[0].field, filter[0].operator, filter[0].value
        var filterMap = InternalCollectIndexed(d, "filter");
        var children = new List<FilterNode>();
        foreach (var (_, fields) in filterMap.OrderBy(x => x.Key))
        {
            var field = fields.TryGetValue("field", out var f) ? f : null;
            if (string.IsNullOrWhiteSpace(field)) continue;
            children.Add(new FilterConditionNode
            {
                Field    = field,
                Operator = FilterOperators.Normalize(fields.TryGetValue("operator", out var o) ? o : "eq"),
                Value    = fields.TryGetValue("value", out var v) ? v : null
            });
        }

        if (children.Count > 0)
            options.Filter = new FilterGroupNode { Logic = logic, Children = children };

        // Collect indexed sorts: sort[0].field, sort[0].desc
        var sortMap = InternalCollectIndexed(d, "sort");
        foreach (var (_, fields) in sortMap.OrderBy(x => x.Key))
        {
            var field = fields.TryGetValue("field", out var f) ? f : null;
            if (string.IsNullOrWhiteSpace(field)) continue;
            options.Sort.Add(new SortNode
            {
                Field      = field,
                Descending = InternalParseBool(fields.TryGetValue("desc", out var dsc) ? dsc : null)
            });
        }
        
        if (d.TryGetValue("sort", out var sortRaw))
            options.Sort.AddRange(InternalParseSort(sortRaw));

        // Metadata
        var incCountStr = d.TryGetValue("includeCount", out var ic) ? ic : null;
        options.IncludeCount = InternalParseBool(incCountStr, true);
        
        var distinctStr = d.TryGetValue("distinct", out var dist) ? dist : null;
        options.Distinct     = InternalParseBool(distinctStr);

        return options;
    }

    internal static void InternalParseSelectWithAggregates(QueryOptions options, string? rawSelect)
    {
        var fields = InternalSplitCsv(rawSelect);
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

            var fn = match.Groups["fn"].Success
                ? match.Groups["fn"].Value.ToLowerInvariant()
                : match.Groups["fn2"].Value.ToLowerInvariant();

            var aggregateField = match.Groups["field"].Success
                ? match.Groups["field"].Value
                : match.Groups["field2"].Success 
                    ? match.Groups["field2"].Value 
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

    internal static HavingCondition? InternalParseHaving(string? rawHaving)
    {
        if (string.IsNullOrWhiteSpace(rawHaving)) return null;
        var match = HavingPattern.Match(rawHaving.Trim());
        if (!match.Success) return null;
 
        var fn = match.Groups["fn"].Value.ToLowerInvariant();
        var field = match.Groups["field"].Success 
            ? match.Groups["field"].Value 
            : (match.Groups["field2"].Success ? match.Groups["field2"].Value : null);
 
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

    internal static QueryOptions InternalParseJsonFilter(QueryOptions options, string json)
    {
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

    internal static QueryOptions InternalParseJsonFilter(IDictionary<string, string> d)
    {
        var options = new QueryOptions();

        // Paging & select same as generic
        options.Paging.Page     = InternalParseInt(d, "page", 1);
        options.Paging.PageSize = InternalParseInt(d, "pageSize", 20);
        if (d.TryGetValue("select", out var sel)) options.Select = InternalSplitCsv(sel);

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

    private static FilterGroupNode ParseJsonGroup(JsonElement root)
    {
        var group = new FilterGroupNode();

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
                    group.Children.Add(ParseJsonGroup(item));
                    continue;
                }

                var field = item.TryGetProperty("field",    out var f) ? f.GetString() : null;
                var op    = item.TryGetProperty("operator", out var o) ? o.GetString() : "eq";
                var value = item.TryGetProperty("value",    out var v)
                    ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText())
                    : null;

                if (!string.IsNullOrWhiteSpace(field))
                    group.Children.Add(new FilterConditionNode
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

    internal static QueryOptions InternalParseDslFilter(IDictionary<string, string> d)
    {
        var options = InternalParseGeneric(d);
        if (!d.TryGetValue("filter", out var filter)) return options;

        try
        {
            var ast = DslParser.Parse(filter);
            options.Filter = DslFilterConverter.ToFilterGroup(ast);
            options.Ast = ast;
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
    internal static QueryOptions InternalParseJql(IDictionary<string, string> d, string query)
    {
        var options = InternalParseGeneric(d);

        var ast = Jql.JqlParser.Parse(query);
        options.Filter = JqlFilterConverter.ToFilterGroup(ast);
        options.Ast = ast;
        options.Filter = Builders.FilterNormalizer.NormalizeOrder(options.Filter);

        return options;
    }



    // ── Shared helpers ────────────────────────────────────────────────────

    private static bool TryParseIndexedKey(
        ReadOnlySpan<char> key,
        ReadOnlySpan<char> prefix,
        out int index,
        out string subKey)
    {
        index = 0;
        subKey = string.Empty;

        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var pos = prefix.Length;
        if (pos >= key.Length || key[pos++] != '[') return false;

        var start = pos;
        while (pos < key.Length && char.IsDigit(key[pos])) pos++;
        if (start == pos || pos >= key.Length || key[pos++] != ']') return false;
        
        #if NET6_0_OR_GREATER
        if (!int.TryParse(key[start..(pos - 1)], out index)) return false;
        #else
        if (!int.TryParse(key[start..(pos - 1)].ToString(), out index)) return false;
        #endif

        if (pos >= key.Length || (key[pos] != '.' && key[pos] != '[')) return false;
        pos++;

        var subStart = pos;
        while (pos < key.Length && key[pos] != ']' && !char.IsWhiteSpace(key[pos])) pos++;
        if (subStart == pos) return false;

        subKey = key[subStart..pos].ToString().ToLowerInvariant();
        return true;
    }



    internal static int InternalParseInt(IDictionary<string, string> d, string key, int defaultValue)
        => d.TryGetValue(key, out var raw) && int.TryParse(raw, out var val) ? val : defaultValue;

    internal static bool InternalParseBool(string? raw, bool defaultValue = false)
        => raw is not null 
            ? (raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1")
            : defaultValue;

    private static LogicOperator ParseLogic(string? raw)
        => string.Equals(raw?.Trim(), "or", StringComparison.OrdinalIgnoreCase)
            ? LogicOperator.Or
            : LogicOperator.And;

    internal static List<string> InternalSplitCsv(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];

        ReadOnlySpan<char> span = raw.AsSpan();
        var result = new List<string>();

        while (!span.IsEmpty)
        {
            var comma = span.IndexOf(',');
            var part = comma < 0 ? span : span[..comma];
            part = part.Trim();

            if (!part.IsEmpty)
                result.Add(part.ToString());

            if (comma < 0) break;
            span = span[(comma + 1)..];
        }

        return result;
    }

    private static List<SortNode> ParseGenericSorts(IDictionary<string, string> d)
    {
        var result = new List<SortNode>();

        var sortMap = InternalCollectIndexed(d, "sort");
        foreach (var (_, fields) in sortMap.OrderBy(x => x.Key))
        {
            var field = fields.TryGetValue("field", out var f) ? f : null;
            if (string.IsNullOrWhiteSpace(field)) continue;
            result.Add(new SortNode
            {
                Field = field,
                Descending = InternalParseBool(fields.TryGetValue("desc", out var dsc) ? dsc : null)
            });
        }

        if (d.TryGetValue("sort", out var sortRaw))
            result.AddRange(InternalParseSort(sortRaw));

        return result;
    }

    internal static List<SortNode> InternalParseSort(string? sortRaw)
    {
        var result = new List<SortNode>();
        if (string.IsNullOrWhiteSpace(sortRaw)) return result;

        ReadOnlySpan<char> span = sortRaw.AsSpan();

        while (!span.IsEmpty)
        {
            var comma = span.IndexOf(',');
            var item = comma < 0 ? span : span[..comma];
            item = item.Trim();

            if (!item.IsEmpty)
            {
                var colon = item.IndexOf(':');
                var fieldSpan = colon < 0 ? item : item[..colon];
                fieldSpan = fieldSpan.Trim();

                if (!fieldSpan.IsEmpty)
                {
                    var field = fieldSpan.ToString();
                    var direction = colon < 0 ? "asc" : item[(colon + 1)..].Trim().ToString();
                    var isDesc = direction.Equals("desc", StringComparison.OrdinalIgnoreCase);

                    var aggregateMatch = AggregateSortPattern.Match(field);
                    if (aggregateMatch.Success)
                    {
                        var aggregate = aggregateMatch.Groups["fn"].Value.ToLowerInvariant();
                        var collection = aggregateMatch.Groups["collection"].Value;
                        var aggregateField = aggregateMatch.Groups["field"].Success
                            ? aggregateMatch.Groups["field"].Value
                            : null;

                        result.Add(new SortNode
                        {
                            Field = collection,
                            Descending = isDesc,
                            Aggregate = aggregate,
                            AggregateField = string.IsNullOrWhiteSpace(aggregateField) ? null : aggregateField
                        });
                    }
                    else
                    {
                        result.Add(new SortNode
                        {
                            Field = field,
                            Descending = isDesc
                        });
                    }
                }
            }

            if (comma < 0) break;
            span = span[(comma + 1)..];
        }

        return result;
    }
}
