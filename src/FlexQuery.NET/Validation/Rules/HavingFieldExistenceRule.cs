using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Resolvers;
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
        CollectErrors(options.Having, context, errors);

        foreach (var error in errors)
        {
            result.Errors.Add(new ValidationError(error, ValidationErrorCodes.FieldNotFound));
        }

        return;

        static void CollectErrors(HavingNode node, QueryContext context, List<string> errors)
        {
            while (true)
            {
                switch (node)
                {
                    case HavingConditionNode c:
                        if (string.IsNullOrWhiteSpace(c.Field)) break;

                        if (!FieldResolver.TryResolvePublicType(
                                context.QuerySurface, context.TargetType!, c.Field, context.ExecutionOptions, out var fieldType))
                        {
                            errors.Add($"Field '{c.Field}' referenced in HAVING does not exist on type '{context.TargetType!.Name}'.");
                            break;
                        }

                        if (TypeHelper.IsNavigationProperty(fieldType))
                        {
                            errors.Add($"Field '{c.Field}' referenced in HAVING is a navigation property. Only scalar properties can be aggregated.");
                        }

                        break;
                    case HavingLogicalNode l:
                        foreach (var child in l.Children) CollectErrors(child, context, errors);
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
