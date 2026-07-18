namespace FlexQuery.NET.Exceptions;

/// <summary>
/// The exception that is thrown when a FlexQuery query cannot be parsed due to
/// invalid syntax or malformed query grammar.
/// </summary>
/// <remarks>
/// This exception represents parser-level errors only, such as invalid
/// identifiers, malformed property paths, unsupported syntax, or incomplete
/// expressions. It does not indicate validation, translation, or execution
/// errors.
///
/// Examples include:
/// <list type="bullet">
/// <item><description>Invalid identifier syntax (e.g. <c>1Name</c>, <c>_Name</c>).</description></item>
/// <item><description>Malformed property paths (e.g. <c>Customer..Name</c>).</description></item>
/// <item><description>Unexpected or missing parser tokens.</description></item>
/// <item><description>Invalid aggregate, select, sort, filter, or groupBy grammar.</description></item>
/// </list>
/// </remarks>
public sealed class FlexQueryParseException(
    string message,
    Exception? innerException = null)
    : FlexQueryException(message, innerException)
{
    /// <summary>Creates a new parse exception with the specified message.</summary>
    /// <param name="message">A description of the grammar violation.</param>
    /// <param name="position">Zero-based character position in the source input, or -1 when unknown.</param>
    public FlexQueryParseException(string message, int position) : this(message)
    {
        Position = position;
    }

    /// <summary>Creates a new parse exception with the specified message, position, and structured metadata.</summary>
    /// <param name="message">A description of the grammar violation.</param>
    /// <param name="position">Zero-based character position in the source input, or -1 when unknown.</param>
    /// <param name="expected">The token or syntax the parser expected, or null.</param>
    /// <param name="found">The token or syntax the parser actually found, or null.</param>
    public FlexQueryParseException(string message, int position, string? expected = null, string? found = null) : this(message)
    {
        Position = position;
        Expected = expected;
        Found = found;
    }

    /// <summary>Zero-based character position in the source input, or -1 when unknown.</summary>
    public int Position { get; } = -1;

    /// <summary>The token or syntax the parser expected, or null.</summary>
    public string? Expected { get; }

    /// <summary>The token or syntax the parser actually found, or null.</summary>
    public string? Found { get; }
}
