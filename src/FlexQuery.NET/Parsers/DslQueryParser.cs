using FlexQuery.NET.Constants;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Filters;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers.Dsl;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Default implementation of <see cref="IQueryParser"/> that handles the native FlexQuery DSL.
/// </summary>
internal sealed class DslQueryParser : IQueryParser
{
    /// <inheritdoc />
    public QuerySyntax Syntax => QuerySyntax.NativeDsl;

    /// <inheritdoc />
    public QueryOptions Parse(FlexQueryParameters parameters)
    {
        QueryOptions options;
        try
        {
            options = QueryOptionsFactory.Create(parameters);
        }
        catch (DslParseException ex)
        {
            throw new QueryParseException(
                InferParameterName(parameters, ex),
                QuerySyntax.NativeDsl,
                InferParameterValue(parameters, ex),
                ex);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Select))
        {
            try
            {
                DslSelectParser.Parse(options, parameters.Select);
            }
            catch (DslParseException ex)
            {
                throw new QueryParseException(QueryOptionKeys.Select, QuerySyntax.NativeDsl, parameters.Select, ex);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Filter))
        {
            try
            {
                var ast = DslAstParser.Parse(parameters.Filter.TrimStart());
                options.Filter = DslFilterConverter.ToFilterGroup(ast);
                if (!parameters.PreserveRawOrder)
                    options.Filter = FilterNormalizer.NormalizeOrder(options.Filter);
            }
            catch (DslParseException ex)
            {
                throw new QueryParseException(QueryOptionKeys.Filter, QuerySyntax.NativeDsl, parameters.Filter, ex);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Include))
        {
            try
            {
                options.Includes = DslIncludeParser.Parse(parameters.Include);
            }
            catch (DslParseException ex)
            {
                throw new QueryParseException(QueryOptionKeys.Include, QuerySyntax.NativeDsl, parameters.Include, ex);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Sort))
        {
            try
            {
                options.Sort.Clear();
                options.Sort.AddRange(SortParser.Parse(parameters.Sort));
            }
            catch (DslParseException ex)
            {
                throw new QueryParseException(QueryOptionKeys.Sort, QuerySyntax.NativeDsl, parameters.Sort, ex);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Aggregate))
        {
            try
            {
                options.Aggregates.Clear();
                options.Aggregates.AddRange(DslAggregateParser.Parse(parameters.Aggregate));
            }
            catch (DslParseException ex)
            {
                throw new QueryParseException(QueryOptionKeys.Aggregate, QuerySyntax.NativeDsl, parameters.Aggregate, ex);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Having))
        {
            try
            {
                options.Having = DslHavingParser.Parse(parameters.Having);
            }
            catch (DslParseException ex)
            {
                throw new QueryParseException(QueryOptionKeys.Having, QuerySyntax.NativeDsl, parameters.Having, ex);
            }
        }

        return options;
    }

    private static string InferParameterName(FlexQueryParameters parameters, DslParseException ex)
    {
        var msg = ex.Message.ToLowerInvariant();
        if (msg.Contains("'select'")) return QueryOptionKeys.Select;
        if (msg.Contains("'group'")) return QueryOptionKeys.GroupBy;
        if (msg.Contains("'include'") || msg.Contains("include expression")) return QueryOptionKeys.Include;
        if (msg.Contains("aggregate")) return QueryOptionKeys.Aggregate;
        if (msg.Contains("having")) return QueryOptionKeys.Having;
        if (msg.Contains("sort")) return QueryOptionKeys.Sort;
        return "parameter";
    }

    private static string? InferParameterValue(FlexQueryParameters parameters, DslParseException ex)
    {
        var msg = ex.Message.ToLowerInvariant();
        if (msg.Contains("'select'") || msg.Contains("select")) return parameters.Select;
        if (msg.Contains("'group'") || msg.Contains("group")) return parameters.GroupBy;
        if (msg.Contains("aggregate")) return parameters.Aggregate;
        if (msg.Contains("having")) return parameters.Having;
        if (msg.Contains("sort")) return parameters.Sort;
        if (msg.Contains("include")) return parameters.Include;
        return null;
    }
}
