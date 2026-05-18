using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers.Dsl;
using FlexQuery.NET.Parsers.Jql;
using FlexQuery.NET.Builders;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Default implementation of <see cref="IQueryParser"/> that handles the native FlexQuery DSL.
/// </summary>
public sealed class FlexQueryDslParser : IQueryParser
{
    /// <inheritdoc />
    public QuerySyntax Syntax => QuerySyntax.NativeDsl;

    /// <inheritdoc />
    public bool CanParse(FlexQueryParameters parameters)
    {
        // Native DSL is the fallback, but we can specifically check for non-OData keys
        // or just return true if it's not obviously OData.
        if (parameters.RawParameters != null)
        {
            foreach (var key in parameters.RawParameters.Keys)
            {
                if (key.StartsWith("$")) return false;
            }
        }
        return true;
    }

    /// <inheritdoc />
    public QueryOptions Parse(FlexQueryParameters parameters)
    {
        if (parameters.RawParameters != null && parameters.RawParameters.Count > 0)
        {
            return QueryOptionsParser.InternalParseDictionary(parameters.RawParameters);
        }

        var options = new QueryOptions();

        // Paging
        options.Paging.Page = parameters.Page ?? 1;
        options.Paging.PageSize = parameters.PageSize ?? 20;

        // Mode
        if (!string.IsNullOrWhiteSpace(parameters.Mode))
        {
            options.ProjectionMode = parameters.Mode.Trim().ToLowerInvariant() switch
            {
                "flat" => ProjectionMode.Flat,
                "flat-mixed" => ProjectionMode.FlatMixed,
                _ => ProjectionMode.Nested
            };
        }

        // Select
        if (!string.IsNullOrWhiteSpace(parameters.Select))
        {
            // Note: We use the helper from QueryOptionsParser which we'll make internal/accessible
            QueryOptionsParser.InternalParseSelectWithAggregates(options, parameters.Select);
        }

        // Grouping
        if (!string.IsNullOrWhiteSpace(parameters.GroupBy))
        {
            options.GroupBy = QueryOptionsParser.InternalSplitCsv(parameters.GroupBy);
        }

        // Having
        if (!string.IsNullOrWhiteSpace(parameters.Having))
        {
            options.Having = QueryOptionsParser.InternalParseHaving(parameters.Having);
        }

        // Includes
        if (!string.IsNullOrWhiteSpace(parameters.Include))
        {
            options.Includes = QueryOptionsParser.InternalSplitCsv(parameters.Include.Split('(')[0]);
            options.FilteredIncludes = FilteredIncludeParser.Parse(parameters.Include);
        }

        // Metadata
        options.IncludeCount = parameters.IncludeCount ?? true;
        options.Distinct = parameters.Distinct ?? false;

        // Sorting
        if (!string.IsNullOrWhiteSpace(parameters.Sort))
        {
            options.Sort.AddRange(QueryOptionsParser.InternalParseSort(parameters.Sort));
        }

        // Filters
        if (!string.IsNullOrWhiteSpace(parameters.Filter))
        {
            var filterVal = parameters.Filter.TrimStart();
            if (filterVal.StartsWith('{'))
            {
                // JSON parsing logic remains in QueryOptionsParser for now or moved here
                QueryOptionsParser.InternalParseJsonFilter(options, filterVal);
            }
            else
            {
                try
                {
                    var ast = DslParser.Parse(filterVal);
                    options.Filter = DslFilterConverter.ToFilterGroup(ast);
                    options.Ast = ast;
                    options.Filter = FilterNormalizer.NormalizeOrder(options.Filter);
                }
                catch (DslParseException) { /* ignore invalid DSL */ }
            }
        }

        return options;
    }
}
