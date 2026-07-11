using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.MiniOData;

namespace FlexQuery.NET.Parsers.MiniOData;

/// <summary>
/// Registers the Mini OData query parser with FlexQuery.
/// Must be called during application startup before executing MiniOData queries.
/// </summary>
public static class MiniODataParser
{
    /// <summary>
    /// Registers the Mini OData parser in the global parser registry.
    /// After calling this method, the parser is available for use
    /// when <see cref="QuerySyntax.MiniOData"/> is configured.
    /// </summary>
    public static void Register()
    {
        QueryParserRegistry.Register(QuerySyntax.MiniOData, new MiniODataQueryParser());
    }
}
