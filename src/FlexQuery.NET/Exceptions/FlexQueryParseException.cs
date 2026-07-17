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
    : FlexQueryException(message, innerException);