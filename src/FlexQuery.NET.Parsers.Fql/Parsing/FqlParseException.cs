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
        Position = -1;
    }

    /// <summary>Creates a <see cref="FqlParseException"/> with the specified error message and character position.</summary>
    /// <param name="message">A description of the Fql grammar violation.</param>
    /// <param name="position">Zero-based character position in the source input, or -1 when unknown.</param>
    public FqlParseException(string message, int position) : base(message)
    {
        Position = position;
    }

    /// <summary>Creates a <see cref="FqlParseException"/> with the specified error message, position, and structured metadata.</summary>
    /// <param name="message">A description of the Fql grammar violation.</param>
    /// <param name="position">Zero-based character position in the source input, or -1 when unknown.</param>
    /// <param name="expected">The token or syntax the parser expected, or null.</param>
    /// <param name="found">The token or syntax the parser actually found, or null.</param>
    public FqlParseException(string message, int position, string? expected = null, string? found = null) : base(message)
    {
        Position = position;
        Expected = expected;
        Found = found;
    }

    /// <summary>Zero-based character position in the source input, or -1 when unknown.</summary>
    public int Position { get; }

    /// <summary>The token or syntax the parser expected, or null.</summary>
    public string? Expected { get; }

    /// <summary>The token or syntax the parser actually found, or null.</summary>
    public string? Found { get; }
}
