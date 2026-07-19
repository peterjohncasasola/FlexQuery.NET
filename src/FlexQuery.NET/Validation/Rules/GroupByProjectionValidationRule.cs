using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that when <see cref="QueryOptions.GroupBy"/> is present and
/// <see cref="QueryOptions.Select"/> is explicitly provided, every non-aggregate
/// projected field exists in the group-by set.
/// </summary>
internal sealed class GroupByProjectionValidationRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.GroupBy is not { Count: > 0 }) return;
        if (options.Select is not { Count: > 0 }) return;

        foreach (var projection in options.Select)
        {
            if (string.Equals(projection.Field, "*", StringComparison.Ordinal))
            {
                result.Errors.Add(new ValidationError(
                    "select=* cannot be used together with groupBy.",
                    ValidationErrorCodes.GroupByWildcardNotAllowed,
                    projection.Field));
                continue;
            }

            if (!options.GroupBy.Contains(projection.Field))
            {
                result.Errors.Add(new ValidationError(
                    $"Field '{projection.Field}' is selected but is not included in groupBy.",
                    ValidationErrorCodes.GroupByProjectionMismatch,
                    projection.Field));
            }
        }
    }
}
