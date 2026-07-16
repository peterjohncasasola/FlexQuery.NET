using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Validation.Rules;

// TODO: Once the dedicated expand feature is implemented, extract the common
// navigation-path validation into a provider-agnostic NavigationPathValidationRule.
// Include and expand will both consume it. Expand will additionally require its own
// ExpandQueryValidationRule for filtering, sorting, paging, and projection rules.
internal sealed class ExpandPathValidationRule : IValidationRule
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
                if (!IsValidNavigationPath(type, include, out var pathNotFound))
                {
                    var code = pathNotFound ? ValidationErrorCodes.IncludePathNotFound : ValidationErrorCodes.NavigationPropertyRequired;
                    result.Errors.Add(new ValidationError(
                        pathNotFound
                            ? $"Include path '{include}' does not exist on type '{type.Name}'."
                            : $"Include path '{include}' contains one or more scalar properties. Only navigation properties are allowed.",
                        code, include));
                }
            }
        }

        if (options.Expand != null)
        {
            foreach (var node in options.Expand)
            {
                ValidateIncludeNode(node, type, string.Empty, result);
            }
        }
    }

    private static void ValidateIncludeNode(IncludeNode node, Type type, string parentPath, ValidationResult result)
    {
        var fullPath = string.IsNullOrEmpty(parentPath) ? node.Path : $"{parentPath}.{node.Path}";

        if (!IsValidNavigationPath(type, node.Path, out var pathNotFound))
        {
            var code = pathNotFound ? ValidationErrorCodes.IncludePathNotFound : ValidationErrorCodes.NavigationPropertyRequired;
            result.Errors.Add(new ValidationError(
                pathNotFound
                    ? $"Expand path '{fullPath}' does not exist on type '{type.Name}'."
                    : $"Expand path '{fullPath}' contains one or more scalar properties. Only navigation properties are allowed.",
                code, fullPath));
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

    private static bool IsValidNavigationPath(Type type, string path, out bool pathNotFound)
    {
        if (!SafePropertyResolver.TryResolveChain(type, path, out var chain))
        {
            pathNotFound = true;
            return false;
        }

        pathNotFound = false;
        foreach (var prop in chain)
        {
            if (!IsNavigationProperty(prop.PropertyType))
                return false;
        }

        return true;
    }

    // TODO: Replace this heuristic with metadata-based navigation detection once
    // QueryContext (or a future IncludeValidationContext) exposes the EF Core IModel
    // or equivalent provider-agnostic navigation metadata.
    private static bool IsNavigationProperty(Type type)
    {
        return TypeHelper.IsNavigationProperty(type);
    }
}
