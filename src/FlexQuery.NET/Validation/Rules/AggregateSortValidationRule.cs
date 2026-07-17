using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that aggregate expressions used in sort are declared in the aggregate clause.
/// Sort may reference aggregate expressions but cannot introduce new ones.
/// </summary>
internal sealed class AggregateSortValidationRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Aggregates.Count == 0)
        {
            ValidateNoAggregates(options, result);
            return;
        }

        var lookup = BuildLookup(options.Aggregates);

        foreach (var sort in options.Sort)
        {
            if (!sort.Aggregate.HasValue) continue;

            var sortField = sort.Aggregate.Value == AggregateFunction.Count
                ? sort.Field
                : (sort.AggregateField ?? sort.Field);

            var key = new AggregateKey(sort.Aggregate.Value, sortField);
            if (!lookup.ContainsKey(key))
            {
                result.Errors.Add(new ValidationError(
                    $"Aggregate '{FormatSortAggregate(sort)}' must be declared in the aggregate clause before it can be used in sort.",
                    ValidationErrorCodes.AggregateNotDeclared));
            }
        }
    }

    private static void ValidateNoAggregates(QueryOptions options, ValidationResult result)
    {
        foreach (var sort in options.Sort)
        {
            if (!sort.Aggregate.HasValue) continue;

            result.Errors.Add(new ValidationError(
                $"Aggregate '{FormatSortAggregate(sort)}' must be declared in the aggregate clause before it can be used in sort.",
                ValidationErrorCodes.AggregateNotDeclared));
        }
    }

    private static Dictionary<AggregateKey, AggregateModel> BuildLookup(IReadOnlyCollection<AggregateModel> aggregates)
    {
        var dict = new Dictionary<AggregateKey, AggregateModel>();
        foreach (var aggregate in aggregates)
        {
            var key = new AggregateKey(aggregate.Function, aggregate.Field);
            dict[key] = aggregate;
        }
        return dict;
    }

    private static string FormatSortAggregate(SortNode sort)
    {
        var function = sort.Aggregate!.Value.ToKeyword().ToUpperInvariant();
        var field = string.IsNullOrEmpty(sort.AggregateField) ? "*" : sort.AggregateField;
        return $"{function}({field})";
    }

    private readonly struct AggregateKey : IEquatable<AggregateKey>
    {
        public AggregateFunction Function { get; }
        public string Field { get; }

        public AggregateKey(AggregateFunction function, string? field)
        {
            Function = function;
            Field = field ?? string.Empty;
        }

        public bool Equals(AggregateKey other)
            => Function == other.Function &&
               string.Equals(Field, other.Field, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) => obj is AggregateKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Function, Field.ToUpperInvariant());
    }
}