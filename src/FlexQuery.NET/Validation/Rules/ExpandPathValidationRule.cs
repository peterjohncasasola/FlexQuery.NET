using FlexQuery.NET.Caching;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that every path in <c>Includes</c> and <c>FilteredIncludes</c>
/// corresponds to a reachable navigation property on the target entity type.
/// Walks the <see cref="IncludeNode"/> tree recursively so that deeply nested
/// expand paths (e.g. <c>Orders.Items.Product</c>) are validated at every level.
/// A typo in any segment is caught before the query reaches a provider.
/// </summary>
public sealed class ExpandPathValidationRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        var type = context.TargetType;
        if (type == null) return;

        if (options.Includes != null)
        {
            foreach (var include in options.Includes)
            {
                if (!NavigationPathExists(type, include))
                {
                    result.Errors.Add(new ValidationError(
                        $"Include path '{include}' does not exist on type '{type.Name}'.",
                        ValidationErrorCodes.IncludePathNotFound, include));
                }
            }
        }

        if (options.FilteredIncludes != null)
        {
            foreach (var node in options.FilteredIncludes)
            {
                ValidateIncludeNode(node, type, string.Empty, result);
            }
        }
    }

    private static void ValidateIncludeNode(IncludeNode node, Type type, string parentPath, ValidationResult result)
    {
        var fullPath = string.IsNullOrEmpty(parentPath) ? node.Path : $"{parentPath}.{node.Path}";

        if (!NavigationPathExists(type, node.Path))
        {
            result.Errors.Add(new ValidationError(
                $"Expand path '{fullPath}' does not exist on type '{type.Name}'.",
                ValidationErrorCodes.IncludePathNotFound, fullPath));
            return;
        }

        if (node.Children.Count > 0)
        {
            if (!SafePropertyResolver.TryResolveChain(type, node.Path, out var chain) || chain.Count == 0)
                return;

            var navProp = chain[^1];
            var childType = SafePropertyResolver.TryGetCollectionElementType(navProp.PropertyType, out var elementType)
                ? elementType
                : navProp.PropertyType;

            foreach (var child in node.Children)
            {
                ValidateIncludeNode(child, childType, fullPath, result);
            }
        }
    }

    private static bool NavigationPathExists(Type type, string path)
    {
        if (!SafePropertyResolver.TryResolveChain(type, path, out var chain))
            return false;
        return chain.Count > 0;
    }
}
