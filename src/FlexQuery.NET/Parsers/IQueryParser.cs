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

/// <summary>
/// Specifies the query syntax to use when parsing requests.
/// </summary>
public enum QuerySyntax
{
    /// <summary>
    /// Uses the native FlexQuery DSL (e.g., filter=name:eq:john).
    /// </summary>
    NativeDsl,
    
    /// <summary>
    /// Uses the FQL syntax (e.g. filter=name = 'john').
    /// </summary>
    Fql,

    /// <summary>
    /// Uses the Mini OData compatibility syntax (e.g., $filter=name eq 'john').
    /// </summary>
    MiniOData
}
