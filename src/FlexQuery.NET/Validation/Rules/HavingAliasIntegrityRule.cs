using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that the HAVING condition references an aggregate that is
/// explicitly declared in the Aggregates collection. This prevents silent
/// mismatches when the HAVING aggregate alias does not correspond to any
/// computed aggregate, which would cause a runtime column-not-found error.
/// </summary>
internal sealed class HavingAliasIntegrityRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Having == null) return;
        if ((options.Aggregates?.Count ?? 0) == 0) return;

        var havingAlias = ParserUtilities.BuildAggregateAlias(options.Having.Function, options.Having.Field);
        var match = options.Aggregates != null && options.Aggregates.Any(a =>
            string.Equals(
                ParserUtilities.BuildAggregateAlias(a.Function, a.Field),
                havingAlias,
                StringComparison.Ordinal));

        if (!match)
        {
            result.Errors.Add(new ValidationError(
                $"HAVING references aggregate '{options.Having.Function}({options.Having.Field})' " +
                $"which is not declared in Aggregates. Add an AggregateModel with matching function and field.",
                ValidationErrorCodes.HavingAliasMismatch));
        }
    }
}
