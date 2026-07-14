using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Validation;

/// <summary>
/// Defines a validator that checks <see cref="QueryOptions"/> before execution.
/// </summary>
internal interface IQueryValidator
{
    /// <summary>Validates the provided query options.</summary>
    /// <param name="options">The query options to validate.</param>
    /// <param name="context">The validation context containing metadata and entity type.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure with details.</returns>
    ValidationResult Validate(QueryOptions options, QueryContext context);
}