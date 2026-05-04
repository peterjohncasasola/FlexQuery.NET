namespace FlexQuery.NET.Models;

/// <summary>
/// A framework-agnostic DTO for dynamic queries. 
/// Automatically documented in Swagger UI when XML comments are enabled.
/// </summary>
public class FlexQueryRequest
{
    /// <summary>
    /// Filter expression using DSL (Field:Operator:Value) or JQL.
    /// <para>Supported Operators: eq, neq, gt, lt, ge, le, contains, startswith, endswith.</para>
    /// </summary>
    /// <example>Name:contains:John,Age:gt:18</example>
    public string? Filter { get; set; }

    /// <summary>
    /// Sorting instructions (e.g. 'FieldName:asc' or 'FieldName:desc').
    /// Supports multiple fields separated by commas.
    /// </summary>
    /// <example>CreatedDate:desc,Name:asc</example>
    public string? Sort { get; set; }

    /// <summary>
    /// Comma-separated list of fields to include in the result.
    /// </summary>
    /// <example>Id,Name,Email</example>
    public string? Select { get; set; }

    /// <summary>
    /// The page number to retrieve (1-indexed).
    /// </summary>
    /// <example>1</example>
    public int? Page { get; set; } = 1;

    /// <summary>
    /// The number of items to return per page.
    /// </summary>
    /// <example>20</example>
    public int? PageSize { get; set; } = 20;
}
