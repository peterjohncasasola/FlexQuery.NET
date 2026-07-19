using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that aggregate queries without GROUP BY do not project entity fields.
/// When aggregates are used without grouping, the result is a single grand-total row;
/// projecting entity fields alongside aggregates is semantically undefined.
/// </summary>
internal sealed class AggregateSelectWithoutGroupByValidationRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Aggregates.Count == 0) return;
        if (options.GroupBy is { Count: > 0 }) return;
        if (options.Select is not { Count: > 0 }) return;

        result.Errors.Add(new ValidationError(
            "Entity fields cannot be selected when aggregates are used without GROUP BY.",
            ValidationErrorCodes.AggregateSelectWithoutGroupBy));
    }
}
