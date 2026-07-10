using FlexQuery.NET.Parsers;


namespace FlexQuery.NET.Exceptions;

/// <summary>
/// Thrown when a query parameter value cannot be parsed using the configured query syntax.
/// </summary>
public sealed class QueryParseException : FlexQueryException
{
    public QueryParseException(
        string parameterName,
        QuerySyntax syntax,
        string? receivedValue,
        Exception innerException)
        : base(
            $"Failed to parse the '{parameterName}' parameter using the " +
            $"configured query syntax '{syntax}'.\n\n" +
            $"Received:\n{receivedValue ?? "<null>"}\n\n" +
            $"Expected {syntax} syntax.",
            innerException)
    {
        ParameterName = parameterName;
        Syntax = syntax;
        ReceivedValue = receivedValue;
    }

    public string ParameterName { get; }
    public QuerySyntax Syntax { get; }
    public string? ReceivedValue { get; }
}
