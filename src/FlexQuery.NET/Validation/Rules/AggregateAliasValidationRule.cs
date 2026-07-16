using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates aggregate aliases and targets according to semantic rules.
/// </summary>
internal sealed class AggregateAliasValidationRule : IValidationRule
{
    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "WHERE", "ORDER", "GROUP", "BY", "HAVING", "AGGREGATE",
        "AND", "OR", "NOT", "IN",
        "LIKE", "BETWEEN", "IS", "CONTAINS", "STARTSWITH", "ENDSWITH",
        "ANY", "ALL",
        "ASC", "DESC", "AS",
        "NULL", "TRUE", "FALSE"
    };

    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Aggregates.Count == 0) return;

        foreach (var aggregate in options.Aggregates)
        {
            ValidateInvalidIdentifier(aggregate, result);
            ValidateReservedKeyword(aggregate, result);
        }

        ValidateDuplicateAliases(options.Aggregates, result);
        ValidateAggregateTargets(options.Aggregates, result);
    }

    private static void ValidateInvalidIdentifier(AggregateModel aggregate, ValidationResult result)
    {
        if (aggregate.Alias.Length > 0 && !ParserUtilities.IsValidIdentifier(aggregate.Alias.AsSpan()))
        {
            result.Errors.Add(new ValidationError(
                $"Aggregate alias '{aggregate.Alias}' is not a valid identifier. " +
                "Aliases must start with a letter and contain only letters, digits, and underscores.",
                ValidationErrorCodes.InvalidAlias));
        }
    }

    private static void ValidateReservedKeyword(AggregateModel aggregate, ValidationResult result)
    {
        if (ReservedKeywords.Contains(aggregate.Alias))
        {
            result.Errors.Add(new ValidationError(
                $"Aggregate alias '{aggregate.Alias}' is a reserved keyword and cannot be used as an alias.",
                ValidationErrorCodes.ReservedAlias));
        }
    }

    private static void ValidateDuplicateAliases(IReadOnlyCollection<AggregateModel> aggregates, ValidationResult result)
    {
        var seen = new Dictionary<string, AggregateModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var aggregate in aggregates)
        {
            if (seen.TryGetValue(aggregate.Alias, out var existing))
            {
                result.Errors.Add(new ValidationError(
                    $"Duplicate aggregate alias '{aggregate.Alias}'. " +
                    $"Alias is used by both '{FormatAggregate(existing)}' and '{FormatAggregate(aggregate)}'.",
                    ValidationErrorCodes.DuplicateAlias));
            }
            else
            {
                seen[aggregate.Alias] = aggregate;
            }
        }
    }

    private static void ValidateAggregateTargets(IReadOnlyCollection<AggregateModel> aggregates, ValidationResult result)
    {
        foreach (var aggregate in aggregates)
        {
            if (aggregate.Field is null && aggregate.Function != AggregateFunction.Count)
            {
                result.Errors.Add(new ValidationError(
                    $"Aggregate function {aggregate.Function} does not support wildcard (*) targets. " +
                    $"Only COUNT supports wildcard. Found in '{FormatAggregate(aggregate)}'.",
                    ValidationErrorCodes.InvalidAggregateTarget));
            }
        }
    }

    private static string FormatAggregate(AggregateModel aggregate)
    {
        var function = aggregate.Function.ToKeyword().ToUpperInvariant();
        var field = aggregate.Field ?? "*";
        return $"{function}({field})";
    }
}
