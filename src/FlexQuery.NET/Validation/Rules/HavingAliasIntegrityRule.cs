using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that the HAVING condition references an aggregate that is
/// explicitly declared in the Aggregates collection. This prevents silent
/// mismatches when the HAVING aggregate does not correspond to any computed
/// aggregate, which would cause a runtime column-not-found error.
/// </summary>
internal sealed class HavingAliasIntegrityRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Having == null) return;

        var hasMatchingAggregate = options.Aggregates.Any(a =>
            a.Function == options.Having.Function &&
            string.Equals(a.Field, options.Having.Field, StringComparison.OrdinalIgnoreCase));

        if (!hasMatchingAggregate)
        {
            result.Errors.Add(new ValidationError(
                $"Aggregate expression '{options.Having.Function.ToKeyword()}:{options.Having.Field}' " +
                "is not declared in the aggregate parameter.",
                ValidationErrorCodes.AggregateNotDeclared));
        }
    }
}
