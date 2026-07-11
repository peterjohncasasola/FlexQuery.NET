namespace FlexQuery.NET.Parsers;

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