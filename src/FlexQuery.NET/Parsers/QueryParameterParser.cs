using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers.Dsl;
using FlexQuery.NET.Parsers.Jql.Ast;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// 
/// </summary>
internal static class QueryParameterParser
{
    public static QueryOptions Parse(IDictionary<string, string> dict)
    {
        if (dict.Count == 0) return new QueryOptions();

        if (dict.TryGetValue(QueryOptionKeys.Query, out var jql)
            && !string.IsNullOrWhiteSpace(jql))
        {
            return ParseJql(dict);
        }

        if (dict.Keys.Any(k => k.StartsWith("filter[0]", StringComparison.OrdinalIgnoreCase)))
        {
            return ParseGeneric(dict);
        }

        if (!dict.TryGetValue(QueryOptionKeys.Filter, out var filterVal)
            || string.IsNullOrWhiteSpace(filterVal))
        {
            return ParseGeneric(dict);
        }

        if (filterVal.TrimStart().StartsWith('{'))
        {
            return JsonParser.Parse(dict);
        }
            
        return ParseDsl(dict);

    }
    
    // ── JQL-lite Filter Format ───────────────────────────────────────────
    //  ?query=(name = "john" OR name = "doe") AND age >= 20
    private static QueryOptions ParseJql(IDictionary<string, string> d)
    {
        var options = ParseGeneric(d);
        var jqlQuery = string.Empty;

        if (d.TryGetValue(QueryOptionKeys.Query, out var query)) jqlQuery = query;
        
        var ast = JqlAstParser.Parse(jqlQuery);
        options.Filter = JqlFilterConverter.ToFilterGroup(ast);
        options.Ast = ast;
        options.Filter = Builders.FilterNormalizer.NormalizeOrder(options.Filter);

        return options;
    }
    
    
    // ── Generic Format ───────────────────────────────────────────────────
    //  ?filter[0].field=Name&filter[0].operator=contains&filter[0].value=john
    //  &sort[0].field=Age&sort[0].desc=true&page=1&pageSize=10&select=Name,Email
    //  &logic=and   (optional top-level logic)
    private static QueryOptions ParseGeneric(IDictionary<string, string> d)
    {
        var options = QueryOptionsFactory.CreateBase(d);

        // Select with aggregates
        if (d.TryGetValue(QueryOptionKeys.Select, out var sel))
        {
            SelectParser.Parse(options, sel);
        }

        // Includes — parse both as plain strings (backward-compat) and as
        // structured IncludeNode trees that support inline JQL filters.
        if (d.TryGetValue(QueryOptionKeys.Include, out var inc))
        {
            options.Includes = ParserUtilities.SplitCsv(inc.Split('(')[0]); // plain names only
            options.FilteredIncludes = FilteredIncludeParser.Parse(inc);
        }

        // Collect indexed filters: filter[0].field, filter[0].operator, filter[0].value
        var filterGroup = FilterParser.Parse(d);
        if (filterGroup != null)
        {
            options.Filter = filterGroup;
        }

        // Sort (indexed + string format)
        options.Sort.AddRange(SortParser.Parse(d));

        return options;
    }
    
    // DSL Filter Format
    //  ?filter=(name:eq:john|name:eq:doe)&age:gt:20
    private static QueryOptions ParseDsl(IDictionary<string, string> d)
    {
        var options = ParseGeneric(d);
        if (!d.TryGetValue(QueryOptionKeys.Filter, out var filter)) return options;

        try
        {
            var ast = DslAstParser.Parse(filter);
            options.Filter = DslFilterConverter.ToFilterGroup(ast);
            options.Ast = ast;
        }
        catch (DslParseException)
        {
            options.Filter = null;
        }

        return options;
    }
}