using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that expressions inside an include block are resolved relative to the included entity.
/// Rejects root-prefixed paths like <c>Orders.Status</c> inside <c>orders(...)</c>.
/// </summary>
internal sealed class IncludeExpressionContextValidationRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Includes is not { Count: > 0 }) return;
        if (context.TargetType == null) return;

        foreach (var node in options.Includes)
        {
            ValidateNode(node, context.TargetType, string.Empty, result);
        }
    }

    private static void ValidateNode(
        IncludeNode node,
        Type rootType,
        string parentPath,
        ValidationResult result,
        HashSet<string>? ancestorPaths = null)
    {
        var fullPath = string.IsNullOrEmpty(parentPath) ? node.Path : $"{parentPath}.{node.Path}";

        if (ancestorPaths == null)
            ancestorPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        else
            ancestorPaths = new HashSet<string>(ancestorPaths, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(parentPath))
            ancestorPaths.Add(parentPath);

        if (!SafePropertyResolver.TryResolveChain(rootType, node.Path, out var chain) || chain.Count == 0)
            return;

        var navProp = chain[^1];
        var targetType = SafePropertyResolver.TryGetCollectionElementType(navProp.PropertyType, out var elementType)
            ? elementType
            : navProp.PropertyType;

        if (node.Filter != null)
        {
            ValidateFilterFields(node.Filter, targetType, fullPath, ancestorPaths, result);
        }

        if (node.Sort is { Count: > 0 })
        {
            foreach (var sort in node.Sort)
            {
                ValidateFieldPath(sort.Field, targetType, fullPath, ancestorPaths, result, "sort");
            }
        }

        if (node.Children is { Count: > 0 })
        {
            foreach (var child in node.Children)
            {
                ValidateNode(child, targetType, fullPath, result, ancestorPaths);
            }
        }
    }

    private static void ValidateFilterFields(
        FilterGroup filter,
        Type targetType,
        string includePath,
        HashSet<string> ancestorPaths,
        ValidationResult result)
    {
        foreach (var condition in filter.Filters)
        {
            ValidateFilterCondition(condition, targetType, includePath, ancestorPaths, result);
        }

        foreach (var group in filter.Groups)
        {
            ValidateFilterFields(group, targetType, includePath, ancestorPaths, result);
        }
    }

    private static void ValidateFilterCondition(
        FilterCondition condition,
        Type targetType,
        string includePath,
        HashSet<string> ancestorPaths,
        ValidationResult result)
    {
        if (!string.IsNullOrEmpty(condition.Field))
        {
            ValidateFieldPath(condition.Field, targetType, includePath, ancestorPaths, result, "filter");
        }

        if (condition.ScopedFilter != null)
        {
            ValidateFilterFields(condition.ScopedFilter, targetType, includePath, ancestorPaths, result);
        }
    }

    private static void ValidateFieldPath(
        string fieldPath,
        Type targetType,
        string includePath,
        HashSet<string> ancestorPaths,
        ValidationResult result,
        string context)
    {
        var prefix = includePath + ".";
        if (fieldPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            fieldPath.Equals(includePath, StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add(new ValidationError(
                $"Field '{fieldPath}' in {context} for include path '{includePath}' is incorrectly prefixed with the navigation path. " +
                $"Use '{fieldPath[prefix.Length..]}' instead (resolved relative to the included entity).",
                ValidationErrorCodes.IncludeRootPrefixedPath,
                fieldPath));
        }

        foreach (var ancestor in ancestorPaths)
        {
            var ancestorPrefix = ancestor + ".";
            if (fieldPath.StartsWith(ancestorPrefix, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add(new ValidationError(
                    $"Field '{fieldPath}' in {context} for include path '{includePath}' is incorrectly prefixed with ancestor navigation path '{ancestor}'. " +
                    $"Use '{fieldPath[ancestorPrefix.Length..]}' instead (resolved relative to the included entity).",
                    ValidationErrorCodes.IncludeRootPrefixedPath,
                    fieldPath));
                break;
            }
        }
    }
}
