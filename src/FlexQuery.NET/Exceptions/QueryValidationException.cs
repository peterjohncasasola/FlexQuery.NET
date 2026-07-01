using FlexQuery.NET.Validation;

namespace FlexQuery.NET.Exceptions;

/// <summary>
/// Thrown when a query fails validation.
/// </summary>
public sealed class QueryValidationException : Exception
{
    /// <summary>
    /// The validation result containing errors.
    /// </summary>
    public ValidationResult Result { get; }

    /// <summary>
    /// Creates a new validation exception from a full result.
    /// </summary>
    public QueryValidationException(ValidationResult result)
        : base("Query validation failed. Check the Result property for details.")
    {
        Result = result;
    }

    /// <summary>
    /// Creates a new validation exception for a single field error.
    /// </summary>
    public QueryValidationException(string message)
        : base(message)
    {
        Result = new ValidationResult();
        Result.Errors.Add(new ValidationError(message, "VALIDATION_ERROR"));
    }

    /// <summary>
    /// Creates a new validation exception with a custom message and a full validation result.
    /// </summary>
    /// <param name="message">The error message describing the validation failure.</param>
    /// <param name="result">The validation result containing detailed error information.</param>
    public QueryValidationException(string message, ValidationResult result)
        : base(message)
    {
        Result = result;
    }
}
