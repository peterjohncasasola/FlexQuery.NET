namespace FlexQuery.NET.Models;

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

    /// <summary>
    /// When non-null, this condition represents a scoped collection filter
    /// (<c>orders.any(...)</c> / <c>orders[...]</c> syntax). The filter group
    /// is applied to each element of the collection and all conditions within
    /// it apply to the <strong>same</strong> element.
    /// <para>
    /// <see cref="Operator"/> is set to <c>any</c> or <c>all</c> and
    /// <see cref="Value"/> is left <see langword="null"/> when this property
    /// is populated.
    /// </para>
    /// </summary>
    public FilterGroup? ScopedFilter { get; set; }
}
