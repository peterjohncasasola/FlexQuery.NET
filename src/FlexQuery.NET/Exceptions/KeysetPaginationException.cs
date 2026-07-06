namespace FlexQuery.NET.Exceptions;

/// <summary>
/// Thrown when keyset (cursor-based) pagination encounters an error,
/// such as missing sort fields, cursor cardinality mismatch, or
/// duplicate orderings.
/// </summary>
public sealed class KeysetPaginationException : Exception
{
    /// <summary>A machine-readable reason code for the failure.</summary>
    public string Reason { get; }

    /// <param name="reason">A human-readable description of the pagination error.</param>
    public KeysetPaginationException(string reason) : base(reason)
    {
        Reason = reason;
    }
}
