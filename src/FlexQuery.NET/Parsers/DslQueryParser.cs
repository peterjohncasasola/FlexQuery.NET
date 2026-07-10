using FlexQuery.NET.Exceptions;
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
        var options = QueryOptionsFactory.Create(parameters);

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
                throw new QueryParseException("filter", QuerySyntax.NativeDsl, parameters.Filter, ex);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Include))
        {
            try
            {
                options.Expand = DslIncludeParser.Parse(parameters.Include);
            }
            catch (DslParseException ex)
            {
                throw new QueryParseException("include", QuerySyntax.NativeDsl, parameters.Include, ex);
            }
        }

        return options;
    }
}
