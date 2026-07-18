using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates aggregate declarations: aliases, duplicate definitions, and declaration rules.
/// </summary>
internal sealed class AggregateValidationRule : IValidationRule
{
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
        ValidateDuplicateDefinitions(options.Aggregates, result);
        ValidateAggregateTargets(options.Aggregates, result);
    }

    private static void ValidateInvalidIdentifier(Aggregate aggregate, ValidationResult result)
    {
        if (aggregate.Alias.Length > 0 && !ParserUtilities.IsValidIdentifier(aggregate.Alias.AsSpan()))
        {
            result.Errors.Add(new ValidationError(
                $"Aggregate alias '{aggregate.Alias}' is not a valid identifier. " +
                "Aliases must start with a letter and contain only letters, digits, and underscores.",
                ValidationErrorCodes.InvalidAlias));
        }
    }

    private static void ValidateReservedKeyword(Aggregate aggregate, ValidationResult result)
    {
        if (ReservedKeywordHelper.IsReserved(aggregate.Alias))
        {
            result.Errors.Add(new ValidationError(
                $"Aggregate alias '{aggregate.Alias}' is a reserved keyword and cannot be used as an alias.",
                ValidationErrorCodes.ReservedAlias));
        }
    }

    private static void ValidateDuplicateAliases(IReadOnlyCollection<Aggregate> aggregates, ValidationResult result)
    {
        var seen = new Dictionary<string, Aggregate>(StringComparer.OrdinalIgnoreCase);
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

    private static void ValidateDuplicateDefinitions(IReadOnlyCollection<Aggregate> aggregates, ValidationResult result)
    {
        var seen = new Dictionary<AggregateKey, Aggregate>();
        foreach (var aggregate in aggregates)
        {
            var key = new AggregateKey(aggregate.Function, aggregate.Field);
            if (seen.TryGetValue(key, out var existing))
            {
                result.Errors.Add(new ValidationError(
                    $"Duplicate aggregate definition '{FormatAggregate(aggregate)}'. " +
                    $"Same function and field are already defined as '{FormatAggregate(existing)}'.",
                    ValidationErrorCodes.DuplicateAggregateDefinition));
            }
            else
            {
                seen[key] = aggregate;
            }
        }
    }

    private static void ValidateAggregateTargets(IReadOnlyCollection<Aggregate> aggregates, ValidationResult result)
    {
        foreach (var aggregate in aggregates)
        {
            if (aggregate.Function == AggregateFunction.Count && aggregate.Field == "*")
            {
                result.Errors.Add(new ValidationError(
                    $"COUNT(*) is not supported. Use COUNT(<collection>) or another aggregate over a property instead. Found in '{FormatAggregate(aggregate)}'.",
                    ValidationErrorCodes.InvalidAggregateTarget));
            }
            else if (aggregate.Field is null)
            {
                result.Errors.Add(new ValidationError(
                    $"Aggregate function {aggregate.Function} does not support wildcard (*) targets. " +
                    $"Only COUNT supports wildcard. Found in '{FormatAggregate(aggregate)}'.",
                    ValidationErrorCodes.InvalidAggregateTarget));
            }
        }
    }

    private static string FormatAggregate(Aggregate aggregate)
    {
        var function = aggregate.Function.ToKeyword().ToUpperInvariant();
        var field = aggregate.Field ?? "*";
        return $"{function}({field})";
    }

    private readonly struct AggregateKey(AggregateFunction function, string? field) : IEquatable<AggregateKey>
    {
        private AggregateFunction Function { get; } = function;
        private string Field { get; } = field ?? string.Empty;

        public bool Equals(AggregateKey other)
            => Function == other.Function &&
               string.Equals(Field, other.Field, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) => obj is AggregateKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Function, Field.ToUpperInvariant());
    }
}