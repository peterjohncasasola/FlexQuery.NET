using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that a HAVING clause is only used when a GROUP BY or at least
/// one aggregate is present. A bare HAVING without grouping or aggregation
/// cannot be translated into a meaningful SQL predicate and would be silently
/// dropped at the provider level.
/// </summary>
internal sealed class HavingWithoutGroupByRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Having == null) return;
        if ((options.GroupBy?.Count ?? 0) > 0) return;
        if (options.Aggregates.Count > 0) return;

        result.Errors.Add(new ValidationError(
            "HAVING clause requires a GROUP BY. Set GroupBy to at least one field, or remove the Having condition.",
            ValidationErrorCodes.HavingWithoutGroupBy));
    }
}
