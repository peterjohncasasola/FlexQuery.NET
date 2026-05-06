using FlexQuery.NET.Models;
using FlexQuery.NET.Constants;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that all operators used in filters are recognized and allowed by the registry.
/// </summary>
public sealed class OperatorValidityRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Filter != null)
        {
            ValidateFilterGroup(options.Filter!, result);
        }
    }
    private void ValidateFilterGroup(FilterGroupNode group, ValidationResult result)
    {
        foreach (var child in group.Children)
        {
            if (child is FilterConditionNode filter)
            {
                if (string.IsNullOrWhiteSpace(filter.Operator)) continue;

                if (!FilterOperators.IsSupported(filter.Operator))
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
            else if (child is FilterGroupNode subGroup)
            {
                ValidateFilterGroup(subGroup, result);
            }
        }
    }
}
