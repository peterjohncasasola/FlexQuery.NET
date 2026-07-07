using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Options;
using FlexQuery.NET.Security;
using FlexQuery.NET.Constants;
using System.ComponentModel;
using FlexQuery.NET.Resolvers;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that operators are compatible with the field types (e.g., "contains" on numeric fields)
/// and that values can be converted to the target property types.
/// </summary>
internal sealed class TypeCompatibilityRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (context.TargetType == null || options.Filter == null) return;

        ValidateFilterGroup(options.Filter, context.TargetType, context.ExecutionOptions, result);
    }

    private void ValidateFilterGroup(FilterGroup group, Type entityType, BaseQueryOptions? executionOptions, ValidationResult result)
    {
        foreach (var filter in group.Filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Field) || string.IsNullOrWhiteSpace(filter.Operator)) continue;

            if (FieldResolver.TryResolveType(entityType, filter.Field, executionOptions, out var propertyType))
            {
                var op = filter.Operator.ToLowerInvariant();

                // 1. Check Operator Compatibility
                if (op is FilterOperators.Contains or FilterOperators.StartsWith or FilterOperators.EndsWith or FilterOperators.Like)
                {
                    if (propertyType != typeof(string))
                    {
                        result.Errors.Add(new ValidationError(
                            $"Operator '{op}' is only compatible with string fields, but '{filter.Field}' is '{propertyType.Name}'.",
                            ValidationErrorCodes.TypeMismatch,
                            filter.Field));
                        continue;
                    }
                }

                // 2. Check Value Compatibility (Simple types)
                if (filter.Value != null && op != FilterOperators.Any && op != FilterOperators.All && op != FilterOperators.Count)
                {
                    if (!CanConvert(filter.Value, propertyType))
                    {
                        result.Errors.Add(new ValidationError(
                            $"Value '{filter.Value}' cannot be converted to type '{propertyType.Name}' for field '{filter.Field}'.",
                            ValidationErrorCodes.TypeMismatch,
                            filter.Field));
                    }
                }
            }

            if (filter.ScopedFilter != null)
            {
                if (FieldResolver.TryResolveType(entityType, filter.Field, executionOptions, out var scopedPropertyType) &&
                    SafePropertyResolver.TryGetCollectionElementType(scopedPropertyType, out var elementType))
                {
                    ValidateFilterGroup(filter.ScopedFilter, elementType, executionOptions, result);
                }
            }
        }

        foreach (var subGroup in group.Groups)
        {
            ValidateFilterGroup(subGroup, entityType, executionOptions, result);
        }
    }

    private static bool CanConvert(object value, Type targetType)
    {
        if (value == null) return true;
        
        // Handle Nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType.IsAssignableFrom(value.GetType())) return true;

        try
        {
            var converter = TypeDescriptor.GetConverter(underlyingType);
            
            if (value is string s)
            {
                converter.ConvertFromInvariantString(s);
                return true;
            }
            
            if (converter.CanConvertFrom(value.GetType()))
            {
                converter.ConvertFrom(value);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
