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
        var options = QueryOptionsFactory.Create(parameters);
        ApplyFilter(parameters, options);

        return options;
    }

    private static void ApplyFilter(FlexQueryParameters parameters, QueryOptions options)
    {
        if (string.IsNullOrWhiteSpace(parameters.Filter)) return;

        var filterVal = parameters.Filter.TrimStart();
        if (filterVal.StartsWith('{'))
        {
            JsonParser.Parse(options, filterVal);
            return;
        }

        try
        {
            var ast = DslAstParser.Parse(filterVal);
            options.Filter = DslFilterConverter.ToFilterGroup(ast);

            if (parameters.RawParameters is null)
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
