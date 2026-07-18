using FlexQuery.NET.Exceptions;

namespace FlexQuery.NET.Parsers.Dsl;

/// <summary>
/// Thrown when a DSL (FlexQuery native) expression cannot be parsed.
/// </summary>
/// <remarks>
/// This exception is used internally by DSL parsers (filter, sort, aggregate, having)
/// and is caught by <see cref="FlexQuery.NET.Parsers.DslQueryParser"/> which wraps it in a
/// <see cref="Exceptions.QueryParseException"/> with the parameter name and syntax context.
///
/// Consumers should catch <see cref="Exceptions.QueryParseException"/> rather than this type.
/// </remarks>
public sealed class DslParseException : FlexQueryException
{
    /// <summary>Creates a <see cref="DslParseException"/> with the specified error message.</summary>
    /// <param name="message">A description of the DSL grammar violation.</param>
    public DslParseException(string message) : base(message)
    {
        Position = -1;
    }

    /// <summary>Creates a <see cref="DslParseException"/> with the specified error message and character position.</summary>
    /// <param name="message">A description of the DSL grammar violation.</param>
    /// <param name="position">Zero-based character position in the source input, or -1 when unknown.</param>
    public DslParseException(string message, int position) : base(message)
    {
        Position = position;
    }

    /// <summary>Creates a <see cref="DslParseException"/> with the specified error message, position, and structured metadata.</summary>
    /// <param name="message">A description of the DSL grammar violation.</param>
    /// <param name="position">Zero-based character position in the source input, or -1 when unknown.</param>
    /// <param name="expected">The token or syntax the parser expected, or null.</param>
    /// <param name="found">The token or syntax the parser actually found, or null.</param>
    public DslParseException(string message, int position, string? expected = null, string? found = null) : base(message)
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
