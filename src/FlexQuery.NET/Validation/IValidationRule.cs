using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Validation;

/// <summary>
/// Defines a modular validation rule for the query pipeline.
/// </summary>
internal interface IValidationRule
{
    /// <summary>Executes the validation rule.</summary>
    /// <param name="options">The query options to validate.</param>
    /// <param name="context">The validation context containing metadata and entity type.</param>
    /// <param name="result">The validation result to populate with errors.</param>
    void Validate(QueryOptions options, QueryContext context, ValidationResult result);
}