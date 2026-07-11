namespace FlexQuery.NET.Parsers.Fql;

/// <summary>
/// Registers the FQL query parser with FlexQuery.
/// Must be called during application startup before executing FQL queries.
/// </summary>
public static class Fql
{
    /// <summary>
    /// Registers the FQL parser in the global parser registry.
    /// After calling this method, the parser is available for use
    /// when <see cref="QuerySyntax.Fql"/> is configured.
    /// </summary>
    public static void Register()
    {
        QueryParserRegistry.Register(QuerySyntax.Fql, new FqlQueryParser());
    }
}
