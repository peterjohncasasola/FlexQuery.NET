namespace DynamicQueryable.Parsers.Jql;

/// <summary>Thrown when a JQL-lite query string cannot be parsed.</summary>
public sealed class JqlParseException : Exception
{
    public JqlParseException(string message) : base(message)
    {
    }
}

