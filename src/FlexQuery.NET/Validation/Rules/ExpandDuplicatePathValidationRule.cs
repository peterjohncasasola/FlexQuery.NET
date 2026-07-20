using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that no duplicate expand paths exist at any level in the normalized tree.
/// </summary>
internal sealed class ExpandDuplicatePathValidationRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Expand is not { Count: > 0 }) return;

        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in options.Expand)
        {
            ValidateNode(node, seenPaths, string.Empty, result);
        }
    }

    private static void ValidateNode(
        IncludeNode node,
        HashSet<string> seenPaths,
        string parentPath,
        ValidationResult result)
    {
        var fullPath = string.IsNullOrEmpty(parentPath) ? node.Path : $"{parentPath}.{node.Path}";

        if (!seenPaths.Add(fullPath))
        {
            result.Errors.Add(new ValidationError(
                $"Duplicate expand path '{fullPath}'.",
                ValidationErrorCodes.ExpandDuplicatePath,
                fullPath));
        }

        if (node.Children is not { Count: > 0 }) return;
        foreach (var child in node.Children)
        {
            ValidateNode(child, seenPaths, fullPath, result);
        }
    }
}
