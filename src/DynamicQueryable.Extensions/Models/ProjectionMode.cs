namespace DynamicQueryable.Models;

/// <summary>
/// Defines how projected data should be shaped.
/// </summary>
public enum ProjectionMode
{
    /// <summary>
    /// Retains the original object hierarchy (e.g. root -> children -> grandchildren).
    /// </summary>
    Nested,

    /// <summary>
    /// Flattens nested collections using SelectMany, producing a flat rowset of the leaf entities.
    /// </summary>
    Flat,

    /// <summary>
    /// Flattens nested collections using correlated SelectMany projections, but allows mixing
    /// root scalar fields with fields from deeply nested collections in a single output row.
    /// </summary>
    FlatMixed
}
