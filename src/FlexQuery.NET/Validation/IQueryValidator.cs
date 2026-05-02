using FlexQuery.NET.Models;

namespace FlexQuery.NET.Validation;

/// <summary>
/// Detailed error information for a failed validation rule.
/// </summary>
public sealed record ValidationError(string Message, string Code, string? Field = null);

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

/// <summary>
/// Defines a validator that checks <see cref="QueryOptions"/> before execution.
/// </summary>
public interface IQueryValidator
{
    /// <summary>Validates the provided query options.</summary>
    ValidationResult Validate(QueryOptions options, QueryContext context);
}

/// <summary>
/// Defines a modular validation rule for the query pipeline.
/// </summary>
public interface IValidationRule
{
    /// <summary>Executes the validation rule.</summary>
    void Validate(QueryOptions options, QueryContext context, ValidationResult result);
}
