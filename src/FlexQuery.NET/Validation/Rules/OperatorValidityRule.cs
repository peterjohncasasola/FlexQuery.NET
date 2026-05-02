using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that all operators used in filters are supported by the registry.
/// </summary>
public sealed class OperatorValidityRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate<T>(QueryOptions options, ValidationResult result)
    {
        if (options.Filter != null)
        {
            ValidateFilterGroup(options.Filter, result);
        }
    }

    private void ValidateFilterGroup(FilterGroup group, ValidationResult result)
    {
        foreach (var filter in group.Filters)
        {
            var op = FilterOperators.Normalize(filter.Operator);
            if (!OperatorRegistry.IsAllowed(op))
            {
                result.Errors.Add(new ValidationError(
                    $"Operator '{filter.Operator}' is not supported.", 
                    "INVALID_OPERATOR", 
                    filter.Field));
            }

            if (filter.ScopedFilter != null)
            {
                ValidateFilterGroup(filter.ScopedFilter, result);
            }
        }

        foreach (var subGroup in group.Groups)
        {
            ValidateFilterGroup(subGroup, result);
        }
    }
}
