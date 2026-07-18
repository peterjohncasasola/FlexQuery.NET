using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that operators used in HAVING expressions are comparison operators only.
/// Only eq, neq, gt, gte, lt, lte are allowed.
/// </summary>
internal sealed class HavingOperatorValidityRule : IValidationRule
{
    private static readonly HashSet<string> AllowedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "eq", "neq", "gt", "gte", "lt", "lte"
    };

    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Having == null) return;

        var invalid = new List<string>();
        CollectInvalid(options.Having, invalid);

        foreach (var op in invalid)
        {
            result.Errors.Add(new ValidationError(
                $"Operator '{op}' is not supported in HAVING expressions. Only eq, neq, gt, gte, lt, lte are allowed.",
                ValidationErrorCodes.InvalidOperator,
                op));
        }
    }

    private static void CollectInvalid(HavingNode node, List<string> invalid)
    {
        switch (node)
        {
            case HavingConditionNode c:
                if (!AllowedOperators.Contains(c.Operator))
                    invalid.Add(c.Operator);
                break;
            case HavingLogicalNode l:
                foreach (var child in l.Children)
                    CollectInvalid(child, invalid);
                break;
            case HavingGroupNode g:
                CollectInvalid(g.Inner, invalid);
                break;
        }
    }
}
