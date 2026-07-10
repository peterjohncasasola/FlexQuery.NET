using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Exceptions;

/// <summary>
/// Thrown when no parser has been registered for the configured <see cref="QuerySyntax"/>.
/// </summary>
public sealed class ParserNotRegisteredException : FlexQueryException
{
    public ParserNotRegisteredException(QuerySyntax syntax)
        : base($"The configured query syntax is {syntax}, but no {syntax} parser has been registered.")
    {
        Syntax = syntax;
    }

    public QuerySyntax Syntax { get; }
}
