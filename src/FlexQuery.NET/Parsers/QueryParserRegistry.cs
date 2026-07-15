using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
namespace FlexQuery.NET.Parsers;

/// <summary>
/// Tracks which query parsers are available, keyed by <see cref="QuerySyntax"/>.
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

    private static readonly object _lock = new();

    /// <summary>
    /// Registers an available parser for the given syntax.
    /// </summary>
    public static void Register(QuerySyntax syntax, IQueryParser parser)
    {
        lock (_lock)
        {
            _available[syntax] = parser;
        }
    }

    /// <summary>
    /// Resolves the parser registered for the specified syntax.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no parser has been registered for <paramref name="syntax"/>.
    /// </exception>
    public static IQueryParser Resolve(QuerySyntax syntax)
    {
        lock (_lock)
        {
            _available.TryGetValue(syntax, out var parser);
            return parser ?? throw new ParserNotRegisteredException(syntax);
        }
    }

    /// <summary>Returns true if a parser has been registered for the given syntax.</summary>
    public static bool IsRegistered(QuerySyntax syntax)
    {
        lock (_lock)
        {
            return _available.ContainsKey(syntax);
        }
    }
}
