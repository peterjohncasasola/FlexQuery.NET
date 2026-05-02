namespace FlexQuery.NET.Security;

/// <summary>
/// Defines the type of query operation being performed on a field.
/// </summary>
public enum QueryOperation
{
    /// <summary>
    /// Filtering operation (WHERE clause).
    /// </summary>
    Filter,

    /// <summary>
    /// Sorting operation (ORDER BY clause).
    /// </summary>
    Sort,

    /// <summary>
    /// Selection/Projection operation (SELECT clause).
    /// </summary>
    Select
}
