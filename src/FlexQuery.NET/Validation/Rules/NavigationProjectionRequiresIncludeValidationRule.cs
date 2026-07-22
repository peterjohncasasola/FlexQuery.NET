using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Validation.Rules;

internal sealed class NavigationProjectionRequiresIncludeValidationRule : IValidationRule
{
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Select is not { Count: > 0 })
            return;

        if (options.Includes is null || options.Includes.Count == 0)
        {
            foreach (var node in options.Select)
            {
                ValidateNode(node, string.Empty, options, result);
            }

            return;
        }

        var allowedIncludes = new HashSet<string>(options.Includes, StringComparer.OrdinalIgnoreCase);
        foreach (var node in options.Select)
        {
            ValidateNode(node, string.Empty, options, result, allowedIncludes);
        }
    }

    private static void ValidateNode(
        SelectNode node,
        string parentPath,
        QueryOptions options,
        ValidationResult result,
        HashSet<string>? allowedIncludes = null)
    {
        if (node.Children.Count == 0)
            return;

        var fullPath = string.IsNullOrEmpty(parentPath) ? node.Field : $"{parentPath}.{node.Field}";

        if (allowedIncludes is null || !allowedIncludes.Contains(fullPath))
        {
            result.Errors.Add(new ValidationError(
                $"The navigation property '{fullPath}' is referenced in the select clause but is not included. Add include={fullPath}.",
                ValidationErrorCodes.NavigationProjectionRequiresInclude,
                fullPath));
        }

        foreach (var child in node.Children)
        {
            ValidateNode(child, fullPath, options, result, allowedIncludes);
        }
    }
}
