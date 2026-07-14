namespace FlexQuery.NET.Validation;

/// <summary>
/// Detailed error information for a failed validation rule.
/// </summary>
/// <param name="Message">A human-readable description of the validation error.</param>
/// <param name="Code">A machine-readable error code identifying the type of error.</param>
/// <param name="Field">The field that caused the validation error, if applicable.</param>
public sealed record ValidationError(string Message, string Code, string? Field = null);