using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Validation.Rules;

internal sealed class SelectValidationRule : IValidationRule
{
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Select is not { Count: > 0 })
            return;

        ValidateScope(options.Select, result);
    }

    private static void ValidateScope(IReadOnlyList<SelectNode> siblings, ValidationResult result)
    {
        var seenAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var wildcardSeen = false;

        foreach (var node in siblings)
        {
            ValidateNode(node, result);

            if (!string.IsNullOrEmpty(node.Alias))
            {
                if (!seenAliases.Add(node.Alias))
                    result.Errors.Add(new ValidationError(
                        $"Duplicate alias '{node.Alias}'.",
                        ValidationErrorCodes.DuplicateAlias));
            }

            if (node.Field == "*")
            {
                if (wildcardSeen)
                    result.Errors.Add(new ValidationError(
                        "Duplicate wildcard projection (*).",
                        ValidationErrorCodes.DuplicateWildcard));
                wildcardSeen = true;
            }

            if (node.Children.Count > 0)
                ValidateScope(node.Children, result);
        }
    }

    private static void ValidateNode(SelectNode node, ValidationResult result)
    {
        if (string.IsNullOrEmpty(node.Alias)) return;
        
        if (!ParserUtilities.IsValidIdentifier(node.Alias.AsSpan()))
            result.Errors.Add(new ValidationError(
                $"Alias '{node.Alias}' is not a valid identifier.",
                ValidationErrorCodes.InvalidAlias));

        if (ReservedKeywordHelper.IsReserved(node.Alias))
            result.Errors.Add(new ValidationError(
                $"Alias '{node.Alias}' is a reserved keyword.",
                ValidationErrorCodes.ReservedAlias));
    }
}
