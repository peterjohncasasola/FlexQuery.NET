using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers.Dsl;
using FlexQuery.NET.Filters;

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
        ApplyFilter(parameters, options);

        return options;
    }

    private static void ApplyFilter(FlexQueryParameters parameters, QueryOptions options)
    {
        if (string.IsNullOrWhiteSpace(parameters.Filter)) return;

        try
        {
            var ast = DslAstParser.Parse(parameters.Filter.TrimStart());
            options.Filter = DslFilterConverter.ToFilterGroup(ast);

            if (!parameters.PreserveRawOrder)
            {
                options.Filter = FilterNormalizer.NormalizeOrder(options.Filter);
            }
        }
        catch (DslParseException)
        {
            options.Filter = null;
        }
    }
}
