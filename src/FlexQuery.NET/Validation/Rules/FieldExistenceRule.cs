using FlexQuery.NET.Models;
using FlexQuery.NET.Security;
using FlexQuery.NET.Constants;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that all fields in filters and sorts exist on the target entity.
/// </summary>
internal sealed class FieldExistenceRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (context.TargetType == null) return; // Cannot validate existence without target type

        if (options.Filter != null)
        {
            ValidateFilterGroup(options.Filter, context.TargetType, context.ExecutionOptions, result);
        }

        foreach (var sort in options.Sort)
        {
            if (string.IsNullOrWhiteSpace(sort.Field)) continue;
            if (IsGroupedAggregateAlias(options, sort.Field)) continue;

            if (!Builders.FieldResolver.TryResolveType(context.TargetType, sort.Field, context.ExecutionOptions, out _))
            {
                result.Errors.Add(new ValidationError(
                    $"Field '{sort.Field}' does not exist on type '{context.TargetType.Name}'.", 
                    ValidationErrorCodes.FieldNotFound, 
                    sort.Field));
            }
        }
    }

    private static bool IsGroupedAggregateAlias(QueryOptions options, string field)
        => options.GroupBy is { Count: > 0 }
           && options.Aggregates.Any(aggregate =>
               aggregate.Alias.Equals(field, StringComparison.OrdinalIgnoreCase));

    private void ValidateFilterGroup(FilterGroup group, Type entityType, QueryExecutionOptions? executionOptions, ValidationResult result)
    {
        foreach (var filter in group.Filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Field)) continue;

            if (!Builders.FieldResolver.TryResolveType(entityType, filter.Field, executionOptions, out var propertyType))
            {
                result.Errors.Add(new ValidationError(
                    $"Field '{filter.Field}' does not exist on type '{entityType.Name}'.", 
                    ValidationErrorCodes.FieldNotFound, 
                    filter.Field));
                continue;
            }

            if (filter.ScopedFilter != null)
            {
                if (SafePropertyResolver.TryGetCollectionElementType(propertyType, out var elementType))
                {
                    ValidateFilterGroup(filter.ScopedFilter, elementType, executionOptions, result);
                }
                else
                {
                    result.Errors.Add(new ValidationError(
                        $"Field '{filter.Field}' is not a collection. Scoped filters can only be applied to collections.", 
                        ValidationErrorCodes.NotACollection, 
                        filter.Field));
                }
            }
        }

        foreach (var subGroup in group.Groups)
        {
            ValidateFilterGroup(subGroup, entityType, executionOptions, result);
        }
    }
}
