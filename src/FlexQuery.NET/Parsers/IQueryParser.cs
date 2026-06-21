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
    /// Determines if the provided parameters can be parsed by this parser.
    /// Used for auto-detection.
    /// </summary>
    bool CanParse(FlexQueryParameters parameters);

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
    /// Automatically detects the syntax based on the presence of specific query parameters
    /// (e.g., OData parameters like $filter, $orderby).
    /// </summary>
    AutoDetect,

    /// <summary>
    /// Uses the native FlexQuery DSL (e.g., filter=name:eq:john).
    /// </summary>
    NativeDsl,
    
    /// <summary>
    /// Uses JSON filter payloads (e.g. filter={"logic":"and","filters":[...]}).
    /// </summary>
    Json,
    
    /// <summary>
    /// Uses the generic indexed query-string syntax (e.g. filter[0].field=name).
    /// </summary>
    Generic,
    
    /// <summary>
    /// Uses the JQL syntax (e.g. $filter=name = 'john').
    /// </summary>
    Jql,

    /// <summary>
    /// Uses the Mini OData compatibility syntax (e.g., $filter=name eq 'john').
    /// </summary>
    MiniOData
}
