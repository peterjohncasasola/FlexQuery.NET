namespace FlexQuery.NET.Parsers.Jql;

/// <summary>
/// Thrown when a JQL (Jira Query Language) expression cannot be parsed.
/// </summary>
/// <remarks>
/// Covers all JQL grammar violations including invalid filter syntax, unrecognized
/// operators, malformed sort expressions, aggregate function errors, and having clause
/// parsing failures.
///
/// This exception is caught by <see cref="JqlQueryParser"/> which wraps it in a
/// <see cref="Exceptions.QueryParseException"/> with the parameter name and syntax context.
///
/// Consumers should catch <see cref="Exceptions.QueryParseException"/> rather than this type.
/// </remarks>
public sealed class JqlParseException : Exception
{
    /// <summary>Creates a <see cref="JqlParseException"/> with the specified error message.</summary>
    /// <param name="message">A description of the JQL grammar violation.</param>
    public JqlParseException(string message) : base(message)
    {
    }
}
