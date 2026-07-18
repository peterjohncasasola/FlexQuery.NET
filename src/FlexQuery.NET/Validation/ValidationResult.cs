using System.Text;

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
    
    /// <summary>
    /// Builds a human-readable validation message containing all validation errors.
    /// </summary>
    /// <returns>A formatted validation message.</returns>
    public string ToErrorMessage()
    {
        if (IsValid)
            return string.Empty;

        var sb = new StringBuilder();

        sb.AppendLine($"Query validation failed ({Errors.Count} {(Errors.Count == 1 ? "error" : "errors")}).");
        sb.AppendLine();

        foreach (var error in Errors)
        {
            sb.Append("• ");
            sb.AppendLine(error.Message);
        }

        return sb.ToString().TrimEnd();
    }
}