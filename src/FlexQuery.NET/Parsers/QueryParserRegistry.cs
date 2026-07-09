using FlexQuery.NET.Models;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Tracks which query parsers are available, keyed by <see cref="QuerySyntax"/>.
/// Optional parser packages self-register during DI; the active parser for a request is
/// resolved through <see cref="Resolve"/>.
/// </summary>
/// <remarks>
/// This component owns parser <em>availability</em> only. Parsing orchestration (request
/// model → <see cref="QueryOptions"/>) lives in <see cref="QueryOptionsParser"/>, which
/// delegates resolution here. This keeps the two responsibilities separate.
/// </remarks>
internal static class QueryParserRegistry
{
    private static readonly Dictionary<QuerySyntax, IQueryParser> _available = new()
    {
        [QuerySyntax.NativeDsl] = new DslQueryParser()
    };

    /// <summary>
    /// Registers an available parser for the given syntax. Called by optional parser packages.
    /// </summary>
    public static void Register(QuerySyntax syntax, IQueryParser parser) => _available[syntax] = parser;

    /// <summary>
    /// Resolves the parser registered for <paramref name="syntax"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no parser has been registered for <paramref name="syntax"/>.
    /// </exception>
    public static IQueryParser Resolve(QuerySyntax syntax)
    {
        _available.TryGetValue(syntax, out var parser);
        
        return parser ?? throw new InvalidOperationException($"The configured query syntax is {syntax}, but no {syntax} parser has been registered.");
    }

    /// <summary>Returns true if a parser has been registered for the given syntax.</summary>
    public static bool IsRegistered(QuerySyntax syntax) => _available.ContainsKey(syntax);
}