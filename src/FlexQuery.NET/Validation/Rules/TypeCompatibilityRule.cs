using FlexQuery.NET.Constants;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Models;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that filter values are compatible with the target field types.
/// </summary>
public sealed class TypeCompatibilityRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate<T>(QueryOptions options, ValidationResult result)
    {
        if (options.Filter != null)
        {
            ValidateFilterGroup(options.Filter, typeof(T), result);
        }
    }

    private void ValidateFilterGroup(FilterGroup group, Type entityType, ValidationResult result)
    {
        foreach (var filter in group.Filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Field)) continue;
            if (!SafePropertyResolver.TryResolveChain(entityType, filter.Field, out var chain)) continue;

            var op = FilterOperators.Normalize(filter.Operator);
            
            // Skip checks for operators that don't use standard value conversion
            if (op is FilterOperators.IsNull or FilterOperators.IsNotNull 
                or FilterOperators.Any or FilterOperators.All or FilterOperators.Count)
                continue;

            if (filter.ScopedFilter != null)
            {
                var lastProp = chain[^1];
                if (SafePropertyResolver.TryGetCollectionElementType(lastProp.PropertyType, out var elementType))
                {
                    ValidateFilterGroup(filter.ScopedFilter, elementType, result);
                }
                continue;
            }

            if (filter.Value == null) continue;

            var targetType = chain[^1].PropertyType;
            
            // Handle collection operators (In, NotIn)
            if (op is FilterOperators.In or FilterOperators.NotIn)
            {
                // Values are usually CSV or JSON arrays depending on parser.
                // We'll skip deep validation of collection elements for now to keep it simple.
                continue;
            }

            if (TypeHelper.ConvertValue(filter.Value, targetType) == null && filter.Value != null)
            {
                result.Errors.Add(new ValidationError(
                    $"Value '{filter.Value}' cannot be converted to type '{targetType.Name}'.", 
                    "TYPE_MISMATCH", 
                    filter.Field));
            }
        }

        foreach (var subGroup in group.Groups)
        {
            ValidateFilterGroup(subGroup, entityType, result);
        }
    }
}
