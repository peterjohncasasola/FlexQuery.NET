using FlexQuery.NET.Caching;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.QuerySurface;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates relationship query options against the relationship cardinality defined by
/// the schema/model metadata — never by naming conventions or runtime record counts.
///
/// <para>
/// <c>filter</c>, <c>sort</c>, and <c>take</c> are collection-only operations: an include
/// node carrying any of them must target a collection-valued relationship. Each violating
/// option is reported individually. Single-valued (reference) relationships may be
/// included — with or without an empty options block — and may carry nested includes for
/// further traversal, but collection operations on them are rejected.
/// </para>
///
/// <para>
/// Cardinality is resolved level by level through the query surface (DTO head names) and
/// the entity graph, so every nested <c>include</c> is validated against its own
/// relationship metadata independently.
/// </para>
/// </summary>
internal sealed class IncludeCollectionOptionValidationRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Includes is not { Count: > 0 }) return;
        if (context.TargetType is null) return;

        foreach (var node in options.Includes)
        {
            ValidateNode(node, context.TargetType, context.QuerySurface, string.Empty, isRoot: true, result);
        }
    }

    private static void ValidateNode(
        IncludeNode node,
        Type currentType,
        IQuerySurface? surface,
        string parentPath,
        bool isRoot,
        ValidationResult result)
    {
        var fullPath = string.IsNullOrEmpty(parentPath) ? node.Path : $"{parentPath}.{node.Path}";

        if (!TryResolveNavigation(node.Path, currentType, surface, isRoot, out var propertyType))
            return; // Unresolvable paths are reported by IncludePathValidationRule.

        var isCollection = SafePropertyResolver.TryGetCollectionElementType(propertyType, out var elementType)
                           && elementType is not null;

        if (!isCollection)
        {
            if (node.Filter is not null)
                result.Errors.Add(CollectionOptionError("filter", fullPath, node.Path));

            if (node.Sort is { Count: > 0 })
                result.Errors.Add(CollectionOptionError("sort", fullPath, node.Path));

            if (node.Take.HasValue)
                result.Errors.Add(CollectionOptionError("take", fullPath, node.Path));
        }

        if (node.Children is not { Count: > 0 }) return;

        var childType = isCollection ? elementType! : propertyType;
        foreach (var child in node.Children)
        {
            ValidateNode(child, childType, surface, fullPath, isRoot: false, result);
        }
    }

    private static ValidationError CollectionOptionError(string option, string fullPath, string relationshipName)
        => new(
            $"The '{option}' option can only be used with collection relationships. " +
            $"'{fullPath}' is a single-valued relationship.",
            option switch
            {
                "filter" => ValidationErrorCodes.IncludeFilterOnReference,
                "sort" => ValidationErrorCodes.IncludeSortOnReference,
                _ => ValidationErrorCodes.IncludeTakeOnReference
            },
            fullPath);

    /// <summary>
    /// Resolves the navigation property type for one include level. At the root, DTO-mode
    /// public names resolve through the query surface; deeper levels are entity-level names
    /// on the navigation target (consistent with the include-path contract).
    /// </summary>
    private static bool TryResolveNavigation(
        string path,
        Type currentType,
        IQuerySurface? surface,
        bool isRoot,
        out Type propertyType)
    {
        propertyType = typeof(object);

        if (isRoot && surface?.ResponseType is not null)
        {
            if (!surface.TryResolve(path, out var field))
                return false;

            // Computed scalar mappings have no real navigation backing.
            if (field.ResponseProperty is null && field.MappingKind == FieldMappingKind.Explicit)
                return false;

            propertyType = field.EntityProperty.PropertyType;
            return true;
        }

        if (!ReflectionCache.TryResolvePropertyChain(currentType, path, out var chain) || chain.Count == 0)
            return false;

        propertyType = chain[^1].PropertyType;
        return true;
    }
}
