namespace FlexQuery.NET.Models.Aggregates;

/// <summary>
/// Specifies the aggregate function to apply to a field.
/// </summary>
public enum AggregateFunction
{
    /// <summary>
    /// Calculates the sum of all values.
    /// </summary>
    Sum,

    /// <summary>
    /// Counts the number of matching records or values.
    /// </summary>
    Count,

    /// <summary>
    /// Calculates the average of all values.
    /// </summary>
    Avg,

    /// <summary>
    /// Returns the minimum value.
    /// </summary>
    Min,

    /// <summary>
    /// Returns the maximum value.
    /// </summary>
    Max
}