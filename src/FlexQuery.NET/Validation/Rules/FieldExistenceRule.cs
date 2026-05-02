using FlexQuery.NET.Models;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that all fields in filters and sorts exist on the target entity.
/// </summary>
public sealed class FieldExistenceRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate<T>(QueryOptions options, ValidationResult result)
    {
        if (options.Filter != null)
        {
            ValidateFilterGroup(options.Filter, typeof(T), result);
        }

        foreach (var sort in options.Sort)
        {
            if (string.IsNullOrWhiteSpace(sort.Field)) continue;
            if (!SafePropertyResolver.TryResolveChain(typeof(T), sort.Field, out _))
            {
                result.Errors.Add(new ValidationError(
                    $"Field '{sort.Field}' does not exist on type '{typeof(T).Name}'.", 
                    "FIELD_NOT_FOUND", 
                    sort.Field));
            }
        }
    }

    private void ValidateFilterGroup(FilterGroup group, Type entityType, ValidationResult result)
    {
        foreach (var filter in group.Filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Field)) continue;

            if (!SafePropertyResolver.TryResolveChain(entityType, filter.Field, out var chain))
            {
                result.Errors.Add(new ValidationError(
                    $"Field '{filter.Field}' does not exist on type '{entityType.Name}'.", 
                    "FIELD_NOT_FOUND", 
                    filter.Field));
                continue;
            }

            if (filter.ScopedFilter != null)
            {
                var lastProp = chain[^1];
                if (SafePropertyResolver.TryGetCollectionElementType(lastProp.PropertyType, out var elementType))
                {
                    ValidateFilterGroup(filter.ScopedFilter, elementType, result);
                }
                else
                {
                    result.Errors.Add(new ValidationError(
                        $"Field '{filter.Field}' is not a collection. Scoped filters can only be applied to collections.", 
                        "NOT_A_COLLECTION", 
                        filter.Field));
                }
            }
        }

        foreach (var subGroup in group.Groups)
        {
            ValidateFilterGroup(subGroup, entityType, result);
        }
    }
}
