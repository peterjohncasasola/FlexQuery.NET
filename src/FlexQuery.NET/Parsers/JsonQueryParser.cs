using FlexQuery.NET.Models;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Default implementation of <see cref="IQueryParser"/> that handles JSON filter payloads.
/// </summary>
public sealed class JsonQueryParser : IQueryParser
{
    /// <inheritdoc />
    public QuerySyntax Syntax => QuerySyntax.Json;

    /// <inheritdoc />
    public bool CanParse(FlexQueryParameters parameters) => JsonParser.IsJsonFilter(parameters.Filter);

    /// <inheritdoc />
    public QueryOptions Parse(FlexQueryParameters parameters)
    {
        var options = QueryOptionsFactory.Create(parameters);

        if (JsonParser.IsJsonFilter(parameters.Filter))
        {
            JsonParser.Parse(options, parameters.Filter!.TrimStart());
        }

        return options;
    }
}
