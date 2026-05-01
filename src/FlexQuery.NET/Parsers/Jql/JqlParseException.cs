namespace FlexQuery.NET.Parsers.Jql;

/// <summary>Thrown when a JQL-lite query string cannot be parsed.</summary>
public sealed class JqlParseException : Exception
{
    /// <summary>Creates a new instance of <see cref="JqlParseException"/>.</summary>
    public JqlParseException(string message) : base(message)
    {
    }
}

