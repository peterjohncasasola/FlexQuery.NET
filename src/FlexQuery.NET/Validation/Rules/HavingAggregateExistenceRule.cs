using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that every aggregate referenced in the HAVING expression
/// is declared in the Aggregates collection. Matches by function AND field (case-insensitive).
/// </summary>
internal sealed class HavingAggregateExistenceRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Having == null) return;

        var declared = options.Aggregates
            .Select(a => (a.Function, a.Field))
            .ToList();

        var missing = new List<string>();

        CollectMissing(options.Having, declared, missing);

        foreach (var reference in missing)
        {
            result.Errors.Add(new ValidationError(
                $"HAVING references aggregate '{reference}' which is not declared in the aggregate clause.",
                ValidationErrorCodes.HavingAliasMismatch,
                reference));
        }
    }

    private static void CollectMissing(HavingNode node, List<(AggregateFunction Function, string? Field)> declared, List<string> missing)
    {
        switch (node)
        {
            case HavingConditionNode c:
            {
                var key = (c.Function, c.Field);
                if (!declared.Any(d => d.Function == key.Function && string.Equals(d.Field, key.Field, StringComparison.OrdinalIgnoreCase)))
                {
                    var fn = c.Function.ToKeyword().ToUpperInvariant();
                    var refStr = c.Field is null ? fn : $"{fn}({c.Field})";
                    missing.Add(refStr);
                }

                break;
            }
            case HavingLogicalNode l:
                foreach (var child in l.Children)
                    CollectMissing(child, declared, missing);
                break;
            case HavingGroupNode g:
                CollectMissing(g.Inner, declared, missing);
                break;
        }
    }
}
