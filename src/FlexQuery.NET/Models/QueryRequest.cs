using System.ComponentModel;

namespace FlexQuery.NET.Models;

/// <summary>
/// A standardized Data Transfer Object representing a dynamic query request from a client.
/// This model separates the untrusted user input from the internal execution model (QueryOptions).
/// </summary>
[Obsolete("QueryRequest is deprecated. Use FlexQueryParameters and bind it via [FromQuery].")]
[EditorBrowsable(EditorBrowsableState.Never)]
public class QueryRequest
{
    /// <summary>
    /// Filter expression using DSL (Field:Operator:Value), JQL, or JSON format.
    /// <para>DSL Examples: Name:contains:John, Age:gt:18</para>
    /// <para>JQL Example: (Name = "John" OR Name = "Doe") AND Age >= 20</para>
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
    /// Comma-separated list of fields to select or project.
    /// Supports nested paths and aliases (e.g. "Id, Name, Profile.Bio as Bio").
    /// </summary>
    /// <example>Id,Name,Email</example>
    public string? Select { get; set; }

    /// <summary>
    /// Comma-separated list of navigation properties to eagerly load with all scalars.
    /// For complex filtered includes, use the 'include=' syntax in the filter or query parameters.
    /// </summary>
    /// <example>Orders,Address</example>
    public string? Include { get; set; }

    /// <summary>
    /// Comma-separated list of fields to group by for aggregation.
    /// </summary>
    /// <example>Category,Status</example>
    public string? GroupBy { get; set; }

    /// <summary>
    /// Having condition applied after aggregation (e.g., "sum(Total):gt:1000").
    /// </summary>
    /// <example>sum(Total):gt:1000</example>
    public string? Having { get; set; }

    /// <summary>
    /// A full JQL (Jira-like Query Language) string. 
    /// If provided, this may override or be merged with the 'Filter' parameter.
    /// </summary>
    public string? Query { get; set; }

    /// <summary>
    /// The current page number to retrieve (1-indexed).
    /// </summary>
    /// <example>1</example>
    public int? Page { get; set; } = 1;

    /// <summary>
    /// The number of items to return per page.
    /// </summary>
    /// <example>20</example>
    public int? PageSize { get; set; } = 20;

    /// <summary>
    /// Whether to include the total count in the result metadata.
    /// </summary>
    public bool? IncludeCount { get; set; } = true;

    /// <summary>
    /// Whether to apply a distinct operation to the result set.
    /// </summary>
    public bool? Distinct { get; set; }

    /// <summary>
    /// The projection mode determining how nested data is flattened (e.g., "nested", "flat", "flat-mixed").
    /// </summary>
    public string? Mode { get; set; }
}
