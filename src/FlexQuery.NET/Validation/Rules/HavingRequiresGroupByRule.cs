using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that a HAVING clause is only used when a GROUP BY is present.
/// </summary>
internal sealed class HavingRequiresGroupByRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Having == null ||
            options.GroupBy is { Count: > 0 })
        {
            return;
        }

        result.Errors.Add(new ValidationError(
            "HAVING cannot be used without GROUP BY.",
            ValidationErrorCodes.HavingRequiresGroupBy));
    }
}
