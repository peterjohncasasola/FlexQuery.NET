using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers.Dsl;
using FlexQuery.NET.Builders;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Default implementation of <see cref="IQueryParser"/> that handles the native FlexQuery DSL.
/// </summary>
public sealed class DslQueryParser : IQueryParser
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
            return QueryParameterParser.Parse(parameters.RawParameters);
        }

        var options = new QueryOptions
        {
            Paging =
            {
                // Paging
                Page = parameters.Page ?? 1,
                PageSize = parameters.PageSize ?? 20
            }
        };

        // Mode
        if (!string.IsNullOrWhiteSpace(parameters.Mode))
        {
            options.ProjectionMode = ParserUtilities.ParseProjectionMode(parameters.Mode);
        }

        // Select
        if (!string.IsNullOrWhiteSpace(parameters.Select))
        {
            // Note: We use the helper from QueryOptionsParser which we'll make internal/accessible
            SelectParser.Parse(options, parameters.Select);
        }

        // Grouping
        if (!string.IsNullOrWhiteSpace(parameters.GroupBy))
        {
            options.GroupBy = ParserUtilities.SplitCsv(parameters.GroupBy);
        }

        // Having
        if (!string.IsNullOrWhiteSpace(parameters.Having))
        {
            options.Having = HavingParser.Parse(parameters.Having);
        }

        // Includes
        if (!string.IsNullOrWhiteSpace(parameters.Include))
        {
            options.Includes = ParserUtilities.SplitCsv(parameters.Include.Split('(')[0]);
            options.FilteredIncludes = FilteredIncludeParser.Parse(parameters.Include);
        }

        // Metadata
        options.IncludeCount = parameters.IncludeCount ?? true;
        options.Distinct = parameters.Distinct ?? false;

        // Sorting
        if (!string.IsNullOrWhiteSpace(parameters.Sort))
        {
            options.Sort.AddRange(SortParser.Parse(parameters.Sort));
        }

        // Filters
        if (!string.IsNullOrWhiteSpace(parameters.Filter))
        {
            var filterVal = parameters.Filter.TrimStart();
            if (filterVal.StartsWith('{'))
            {
                // JSON parsing logic remains in QueryOptionsParser for now or moved here
                JsonParser.Parse(options, filterVal);
            }
            else
            {
                try
                {
                    var ast = DslAstParser.Parse(filterVal);
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
