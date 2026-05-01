namespace FlexQuery.NET.Parsers.Dsl;

/// <summary>Thrown when a DSL filter string cannot be parsed.</summary>
public sealed class DslParseException : Exception
{
    /// <summary>Creates a DSL parse exception.</summary>
    public DslParseException(string message) : base(message)
    {
    }
}
