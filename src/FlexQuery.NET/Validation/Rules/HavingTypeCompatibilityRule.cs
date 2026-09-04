using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Resolvers;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that comparison values in HAVING expressions are compatible with the
/// aggregate result type. SUM/AVG/MIN/MAX require numeric values; COUNT requires numeric values.
/// </summary>
internal sealed class HavingTypeCompatibilityRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Having == null) return;
        if (context.TargetType == null) return;

        var errors = new List<string>();
        CollectErrors(options.Having, context.TargetType, context, options.Aggregates, errors);

        foreach (var error in errors)
        {
            result.Errors.Add(new ValidationError(error, ValidationErrorCodes.TypeMismatch));
        }
    }

    private static void CollectErrors(HavingNode node, Type entityType, QueryContext context, List<Aggregate> aggregates, List<string> errors)
    {
        switch (node)
        {
            case HavingConditionNode c:
                ValidateCondition(c, entityType, context, aggregates, errors);
                break;
            case HavingLogicalNode l:
                foreach (var child in l.Children)
                    CollectErrors(child, entityType, context, aggregates, errors);
                break;
            case HavingGroupNode g:
                CollectErrors(g.Inner, entityType, context, aggregates, errors);
                break;
        }
    }

    private static void ValidateCondition(HavingConditionNode condition, Type entityType, QueryContext context, List<Aggregate> aggregates, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(condition.Value)) return;

        Type? targetType = null;

        if (!string.IsNullOrWhiteSpace(condition.Field))
        {
            if (FieldResolver.TryResolvePublicType(
                    context.QuerySurface, entityType, condition.Field, context.ExecutionOptions, out var resolvedType))
            {
                targetType = resolvedType;
            }
        }
        else
        {
            var declared = aggregates.FirstOrDefault(a =>
                a.Function == condition.Function &&
                string.Equals(a.Field, condition.Field, StringComparison.OrdinalIgnoreCase));
            if (declared is not null && !string.IsNullOrWhiteSpace(declared.Field))
            {
                if (FieldResolver.TryResolvePublicType(
                        context.QuerySurface, entityType, declared.Field, context.ExecutionOptions, out var resolvedType))
                {
                    targetType = resolvedType;
                }
            }
        }

        if (targetType is null) return;

        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        var isNumericFunction = condition.Function is AggregateFunction.Sum or AggregateFunction.Avg or AggregateFunction.Min or AggregateFunction.Max;
        var isCountFunction = condition.Function == AggregateFunction.Count;

        if (isNumericFunction && !TypeHelper.IsNumeric(underlying))
        {
            errors.Add($"Value '{condition.Value}' is not compatible with aggregate function '{condition.Function.ToKeyword()}' on non-numeric field '{condition.Field}'.");
            return;
        }

        if (!isCountFunction) return;
        
        if (!long.TryParse(condition.Value, out _) && !int.TryParse(condition.Value, out _))
        {
            errors.Add($"Value '{condition.Value}' is not numeric. COUNT comparisons require numeric values.");
        }
    }
}
