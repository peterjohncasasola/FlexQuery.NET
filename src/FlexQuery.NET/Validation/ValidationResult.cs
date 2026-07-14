namespace FlexQuery.NET.Validation;

/// <summary>
/// Result of a query validation operation.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>True if no errors were found.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>List of validation errors found.</summary>
    public List<ValidationError> Errors { get; } = [];

    /// <summary>Creates a successful validation result.</summary>
    public static ValidationResult Success() => new();
}