using FlexQuery.NET.Models;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Defines the contract for parsing raw query parameters into a unified <see cref="QueryOptions"/> AST.
/// </summary>
public interface IQueryParser
{
    /// <summary>
    /// The syntax type this parser handles.
    /// </summary>
    QuerySyntax Syntax { get; }

    /// <summary>
    /// Parses the raw parameters into a unified <see cref="QueryOptions"/> object.
    /// </summary>
    QueryOptions Parse(FlexQueryParameters parameters);
}