using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Validation.Rules;

    /// <summary>
    /// Validates that sort and take are not applied to reference (non-collection) navigations.
    /// A single entity cannot be meaningfully sorted, and take on a reference navigation
    /// would always return exactly one entity.
    /// </summary>
    /// <remarks>
    /// v4 intentionally excludes <c>skip</c> from expand blocks (deferred to v5).
    /// This rule therefore only validates <c>sort</c> and <c>take</c>.
    /// </remarks>
internal sealed class ExpandSortOnReferenceValidationRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Expand is not { Count: > 0 }) return;
        if (context.TargetType == null) return;

        foreach (var node in options.Expand)
        {
            ValidateNode(node, context.TargetType, string.Empty, result);
        }
    }

    private static void ValidateNode(
        IncludeNode node,
        Type rootType,
        string parentPath,
        ValidationResult result)
    {
        var fullPath = string.IsNullOrEmpty(parentPath) ? node.Path : $"{parentPath}.{node.Path}";

        if (!SafePropertyResolver.TryResolveChain(rootType, node.Path, out var chain) || chain.Count == 0)
            return;

        var navProp = chain[^1];
        var isCollection = SafePropertyResolver.TryGetCollectionElementType(navProp.PropertyType, out _);

        if (!isCollection)
        {
            if (node.Sort is { Count: > 0 })
            {
                result.Errors.Add(new ValidationError(
                    $"Sort is not allowed on reference navigation '{fullPath}'. " +
                    "Sort can only be applied to collection navigations.",
                    ValidationErrorCodes.ExpandSortOnReference,
                    fullPath));
            }

            if (node.Take.HasValue)
            {
                result.Errors.Add(new ValidationError(
                    $"Take is not allowed on reference navigation '{fullPath}'. " +
                    "Take can only be applied to collection navigations.",
                    ValidationErrorCodes.ExpandTakeOnReference,
                    fullPath));
            }
        }

        // Recurse into children
        if (node.Children is { Count: > 0 })
        {
            var childTargetType = isCollection
                ? SafePropertyResolver.TryGetCollectionElementType(navProp.PropertyType, out var elementType)
                    ? elementType
                    : navProp.PropertyType
                : navProp.PropertyType;

            foreach (var child in node.Children)
            {
                ValidateNode(child, childTargetType, fullPath, result);
            }
        }
    }
}
