using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that all operators used in filters are recognized and allowed by the registry.
/// </summary>
internal sealed class OperatorValidityRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Filter != null)
        {
            ValidateFilterGroup(options.Filter!, result, context.ExecutionOptions);
        }
    }
    private void ValidateFilterGroup(FilterGroupNode group, ValidationResult result, QueryGovernanceOptions? execOptions)
    {
        foreach (var child in group.Children)
        {
            if (child is FilterConditionNode filter)
            {
                if (string.IsNullOrWhiteSpace(filter.Operator)) continue;

                var canonicalOp = FilterOperators.Normalize(filter.Operator);

                if (!FilterOperators.IsSupported(canonicalOp))
                {
                    result.Errors.Add(new ValidationError(
                        $"Operator '{filter.Operator}' is not supported.", 
                        ValidationErrorCodes.InvalidOperator, 
                        filter.Field));
                }
                else if (execOptions?.AllowedOperators != null && 
                         execOptions.AllowedOperators.TryGetValue(filter.Field, out var allowedOps))
                {
                    if (!allowedOps.Contains(canonicalOp))
                    {
                        result.Errors.Add(new ValidationError(
                            $"Operator '{filter.Operator}' is not allowed for field '{filter.Field}'.", 
                            ValidationErrorCodes.OperatorNotAllowed, 
                            filter.Field));
                    }
                }

                if (filter.ScopedFilter != null)
                {
                    ValidateFilterGroup(filter.ScopedFilter, result, execOptions);
                }
            }
            else if (child is FilterGroupNode subGroup)
            {
                ValidateFilterGroup(subGroup, result, execOptions);
            }
        }
    }
}

