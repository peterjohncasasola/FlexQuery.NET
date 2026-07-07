namespace FlexQuery.NET.Models.Filters;

/// <summary>
/// A single field-level filter condition.
/// </summary>
public sealed class FilterConditionNode : FilterNode
{
    /// <summary>The property name to filter on.</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>The comparison operator.</summary>
    public string Operator { get; set; } = "eq";

    /// <summary>The value to compare against.</summary>
    public string? Value { get; set; }

    /// <summary>Nested scoped filter for collection navigations.</summary>
    public FilterGroupNode? ScopedFilter { get; set; }
}