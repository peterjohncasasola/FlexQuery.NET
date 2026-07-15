using FlexQuery.NET.Exceptions;

namespace FlexQuery.NET.Parsers.Fql;

/// <summary>
/// Thrown when a FQL (FlexQuery Language) expression cannot be parsed.
/// </summary>
/// <remarks>
/// Covers all Fql grammar violations including invalid filter syntax, unrecognized
/// operators, malformed sort expressions, aggregate function errors, and having clause
/// parsing failures.
///
/// This exception is caught by <see cref="FqlQueryParser"/> which wraps it in a
/// <see cref="Exceptions.QueryParseException"/> with the parameter name and syntax context.
///
/// Consumers should catch <see cref="Exceptions.QueryParseException"/> rather than this type.
/// </remarks>
public sealed class FqlParseException : FlexQueryException
{
    /// <summary>Creates a <see cref="FqlParseException"/> with the specified error message.</summary>
    /// <param name="message">A description of the Fql grammar violation.</param>
    public FqlParseException(string message) : base(message)
    {
    }
}
