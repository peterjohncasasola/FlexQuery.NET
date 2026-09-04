using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Security;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Options;
using FlexQuery.NET.Resolvers;

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
            ValidateFilterGroup(options.Filter, context, result);
        }

        foreach (var sort in options.Sort)
        {
            if (string.IsNullOrWhiteSpace(sort.Field)) continue;
            if (sort.Aggregate.HasValue) continue; // Aggregate sorts are validated by AggregateSortValidationRule
            if (IsGroupedAggregateAlias(options, sort.Field)) continue;

            if (!FieldResolver.TryResolvePublicType(
                    context.QuerySurface, context.TargetType, sort.Field, context.ExecutionOptions, out _))
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

    private void ValidateFilterGroup(FilterGroup group, QueryContext context, ValidationResult result, string? prefix = null)
        => ValidateFilterGroup(group, context.TargetType!, context, result, prefix);

    private void ValidateFilterGroup(
        FilterGroup group,
        Type entityType,
        QueryContext context,
        ValidationResult result,
        string? prefix)
    {
        // Public-surface resolution applies to root-level fields only. Scoped filter
        // fields and nested group fields are entity-level names on their target type.
        var usePublicSurface = prefix is null && context.QuerySurface?.ResponseType != null;

        foreach (var filter in group.Filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Field)) continue;

            var resolves = usePublicSurface
                ? FieldResolver.TryResolvePublicType(
                    context.QuerySurface, entityType, filter.Field, context.ExecutionOptions, out var publicPropertyType)
                : FieldResolver.TryResolveType(entityType, filter.Field, context.ExecutionOptions, out publicPropertyType);

            if (!resolves)
            {
                result.Errors.Add(new ValidationError(
                    $"Field '{filter.Field}' does not exist on type '{entityType.Name}'.",
                    ValidationErrorCodes.FieldNotFound,
                    filter.Field));
                continue;
            }

            if (filter.ScopedFilter != null)
            {
                if (SafePropertyResolver.TryGetCollectionElementType(publicPropertyType, out var elementType))
                {
                    // Scoped filter fields are entity-level names on the element type.
                    ValidateFilterGroup(filter.ScopedFilter, elementType, context, result, prefix ?? filter.Field);
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
            ValidateFilterGroup(subGroup, entityType, context, result, prefix);
        }
    }
}
