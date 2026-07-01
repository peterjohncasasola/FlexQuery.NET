using FlexQuery.NET.Models;

namespace FlexQuery.NET.Validation;

/// <summary>
/// Detailed error information for a failed validation rule.
/// </summary>
/// <param name="Message">A human-readable description of the validation error.</param>
/// <param name="Code">A machine-readable error code identifying the type of error.</param>
/// <param name="Field">The field that caused the validation error, if applicable.</param>
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
    /// <param name="options">The query options to validate.</param>
    /// <param name="context">The validation context containing metadata and entity type.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure with details.</returns>
    ValidationResult Validate(QueryOptions options, QueryContext context);
}

/// <summary>
/// Defines a modular validation rule for the query pipeline.
/// </summary>
public interface IValidationRule
{
    /// <summary>Executes the validation rule.</summary>
    /// <param name="options">The query options to validate.</param>
    /// <param name="context">The validation context containing metadata and entity type.</param>
    /// <param name="result">The validation result to populate with errors.</param>
    void Validate(QueryOptions options, QueryContext context, ValidationResult result);
}

