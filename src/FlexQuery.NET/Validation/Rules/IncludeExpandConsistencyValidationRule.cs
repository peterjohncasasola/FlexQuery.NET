using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that every expand path's full prefix exactly matches an include path.
/// The include list is the authoritative set of navigations the client is permitted to expand.
/// </summary>
internal sealed class IncludeExpandConsistencyValidationRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Expand is not { Count: > 0 }) return;

        // Omitted include list = no restriction on expand.
        // Explicitly empty include list = nothing is allowed, so every expand path is invalid.
        if (options.Includes is null) return;

        if (options.Includes.Count == 0)
        {
            AddErrorForAllExpandPaths(options.Expand, string.Empty, result,
                "Expand is not allowed when include list is empty.");
            return;
        }

        var allowedIncludes = new HashSet<string>(options.Includes, StringComparer.OrdinalIgnoreCase);

        foreach (var node in options.Expand)
        {
            ValidateNode(node, allowedIncludes, string.Empty, result);
        }
    }

    private static void ValidateNode(
        IncludeNode node,
        HashSet<string> allowedIncludes,
        string parentPath,
        ValidationResult result)
    {
        var fullPath = string.IsNullOrEmpty(parentPath) ? node.Path : $"{parentPath}.{node.Path}";

        if (!allowedIncludes.Contains(fullPath))
        {
            result.Errors.Add(new ValidationError(
                $"Expand path '{fullPath}' requires '{fullPath}' in include.",
                ValidationErrorCodes.ExpandPathNotInInclude,
                fullPath));
        }

        if (node.Children is not { Count: > 0 }) return;
        
        foreach (var child in node.Children)
        {
            ValidateNode(child, allowedIncludes, fullPath, result);
        }
    }

    private static void AddErrorForAllExpandPaths(
        List<IncludeNode> expandNodes,
        string parentPath,
        ValidationResult result,
        string message)
    {
        foreach (var node in expandNodes)
        {
            var fullPath = string.IsNullOrEmpty(parentPath) ? node.Path : $"{parentPath}.{node.Path}";
            result.Errors.Add(new ValidationError(message, ValidationErrorCodes.ExpandPathNotInInclude, fullPath));

            if (node.Children is { Count: > 0 })
            {
                AddErrorForAllExpandPaths(node.Children, fullPath, result, message);
            }
        }
    }
}
