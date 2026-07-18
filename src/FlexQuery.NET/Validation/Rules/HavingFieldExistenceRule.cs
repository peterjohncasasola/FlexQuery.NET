using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that fields referenced in HAVING expressions exist on the target entity type
/// and are scalar properties (not navigation properties).
/// </summary>
internal sealed class HavingFieldExistenceRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Having == null) return;
        if (context.TargetType == null) return;

        var errors = new List<string>();
        CollectErrors(options.Having, context.TargetType, errors);

        foreach (var error in errors)
        {
            result.Errors.Add(new ValidationError(error, ValidationErrorCodes.FieldNotFound));
        }

        return;

        static void CollectErrors(HavingNode node, Type entityType, List<string> errors)
        {
            while (true)
            {
                switch (node)
                {
                    case HavingConditionNode c:
                        if (string.IsNullOrWhiteSpace(c.Field)) break;

                        if (!SafePropertyResolver.TryResolveChain(entityType, c.Field, out var chain) || chain.Count == 0)
                        {
                            errors.Add($"Field '{c.Field}' referenced in HAVING does not exist on type '{entityType.Name}'.");
                            break;
                        }

                        var lastProp = chain[^1];
                        if (TypeHelper.IsNavigationProperty(lastProp.PropertyType))
                        {
                            errors.Add($"Field '{c.Field}' referenced in HAVING is a navigation property. Only scalar properties can be aggregated.");
                        }

                        break;
                    case HavingLogicalNode l:
                        foreach (var child in l.Children) CollectErrors(child, entityType, errors);
                        break;
                    case HavingGroupNode g:
                        node = g.Inner;
                        continue;
                }

                break;
            }
        }
    }
}
