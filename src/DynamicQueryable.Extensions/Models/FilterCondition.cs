namespace DynamicQueryable.Models;

/// <summary>
/// A single field-level filter predicate.
/// </summary>
public sealed class FilterCondition
{
    /// <summary>The property name to filter on (supports dot-notation for nested props).</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>The comparison operator (see <see cref="Constants.FilterOperators"/>).</summary>
    public string Operator { get; set; } = "eq";

    /// <summary>The value to compare against (as a raw string; coerced at build time).</summary>
    public string? Value { get; set; }

    /// <summary>Whether this condition should be negated.</summary>
    public bool IsNegated { get; set; }
}
