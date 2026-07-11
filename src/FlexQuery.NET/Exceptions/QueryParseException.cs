using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Exceptions;

/// <summary>
/// Thrown when a supported FlexQuery query parameter is supplied with a value that
/// cannot be parsed using the configured query syntax (NativeDsl, Fql, or MiniOData).
/// </summary>
/// <remarks>
/// This is a top-level exception. The <see cref="FlexQueryException"/> contains
/// parser-specific details from the underlying grammar parser (e.g.,
/// <c>DslParseException</c> or <c>FqlParseException</c>).
///
/// Unknown query parameters (those not in the official FlexQuery set) should never
/// produce this exception — they are ignored by the parser infrastructure.
/// </remarks>
public sealed class QueryParseException : FlexQueryException
{
    /// <summary>
    /// Creates a new <see cref="QueryParseException"/> for a specific parameter.
    /// </summary>
    /// <param name="parameterName">The query parameter name that failed to parse (e.g. "filter", "sort", "select").</param>
    /// <param name="syntax">The configured query syntax that was used during parsing.</param>
    /// <param name="receivedValue">The raw value that was supplied for the parameter, or <c>null</c> if none.</param>
    /// <param name="innerException">The parser-specific exception with grammar-level detail.</param>
    public QueryParseException(
        string parameterName,
        QuerySyntax syntax,
        string? receivedValue,
        Exception innerException)
        : base(
            $"Failed to parse query parameter '{parameterName}'.\n\n" +
            $"Value:\n{receivedValue ?? "<null>"}",
            innerException)
    {
        ParameterName = parameterName;
        Syntax = syntax;
        ReceivedValue = receivedValue;
    }

    /// <summary>The query parameter name that could not be parsed (e.g. "filter", "sort", "select").</summary>
    public string ParameterName { get; }

    /// <summary>The configured query syntax that was active during parsing (NativeDsl, Fql, MiniOData).</summary>
    public QuerySyntax Syntax { get; }

    /// <summary>The raw value that was supplied for the parameter. <c>null</c> when no value was provided.</summary>
    public string? ReceivedValue { get; }
}
