namespace FlexQuery.NET.Parsers.Jql;

/// <summary>
/// Represents an error that occurs during JQL parsing, tokenization, or AST construction.
/// Thrown when the input contains invalid syntax, unsupported operators, or malformed expressions.
/// </summary>
public sealed class JqlParseException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JqlParseException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    public JqlParseException(string message) : base(message)
    {
    }
}
