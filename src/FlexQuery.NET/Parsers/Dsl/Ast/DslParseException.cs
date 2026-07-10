namespace FlexQuery.NET.Parsers.Dsl;

/// <summary>
/// Thrown when a DSL (FlexQuery native) expression cannot be parsed.
/// </summary>
/// <remarks>
/// This exception is used internally by DSL parsers (filter, sort, aggregate, having)
/// and is caught by <see cref="DslQueryParser"/> which wraps it in a
/// <see cref="Exceptions.QueryParseException"/> with the parameter name and syntax context.
///
/// Consumers should catch <see cref="Exceptions.QueryParseException"/> rather than this type.
/// </remarks>
public sealed class DslParseException : Exception
{
    /// <summary>Creates a <see cref="DslParseException"/> with the specified error message.</summary>
    /// <param name="message">A description of the DSL grammar violation.</param>
    public DslParseException(string message) : base(message)
    {
    }
}
