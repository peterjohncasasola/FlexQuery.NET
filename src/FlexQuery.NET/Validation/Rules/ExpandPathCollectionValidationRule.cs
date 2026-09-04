using FlexQuery.NET.Caching;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.QuerySurface;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that expansion (expand) paths are collection-valued, shared by all
/// providers: the terminal navigation of every expansion path must resolve through the
/// query surface / entity graph and be <b>collection-valued</b>. Reference navigations
/// are not expandable (include-only).
///
/// Complements <see cref="ExpandSortOnReferenceValidationRule"/> (which rejects
/// sort/take on reference navigations) and <see cref="ExpandDuplicatePathValidationRule"/>
/// (which rejects duplicate paths). Those rules only fire when sort/take options are
/// present; this rule enforces the collection terminal unconditionally, so a bare
/// <c>expand=Orders.Customer()</c> is rejected as well.
///
/// The head segment resolves through the query surface in DTO mode (renamed public
/// navigation names), then the remainder walks the entity graph. This is a query-language
/// semantic rule; it executes in the shared validation pipeline before provider-specific
/// execution.
/// </summary>
internal sealed class ExpandPathCollectionValidationRule : IValidationRule
{
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Expand is not { Count: > 0 }) return;

        foreach (var node in options.Expand)
        {
            ValidateNode(node, context, string.Empty, result);
        }
    }

    private static void ValidateNode(
        IncludeNode node,
        QueryContext context,
        string parentPath,
        ValidationResult result)
    {
        var fullPath = string.IsNullOrEmpty(parentPath) ? node.Path : $"{parentPath}.{node.Path}";

        if (TryResolveTerminalType(context, fullPath, out var terminalType, out var resolvable)
            && !SafePropertyResolver.TryGetCollectionElementType(terminalType, out _))
        {
            result.Errors.Add(new ValidationError(
                $"Expand path '{fullPath}' targets a reference navigation. Only collection-valued navigations can be expanded.",
                ValidationErrorCodes.NotACollection,
                fullPath));
        }

        if (node.Children is not { Count: > 0 }) return;

        foreach (var child in node.Children)
        {
            ValidateNode(child, context, fullPath, result);
        }
    }

    /// <summary>
    /// Resolves the terminal property type of an expansion path. The head segment is a
    /// public query surface field (DTO mode) or an entity property (entity mode); the
    /// remainder walks the entity graph.
    /// </summary>
    private static bool TryResolveTerminalType(
        QueryContext context,
        string path,
        out Type terminalType,
        out bool resolvable)
    {
        terminalType = typeof(object);
        resolvable = true;

        var targetType = context.TargetType;
        if (targetType is null)
        {
            resolvable = false;
            return false;
        }

        var surface = context.QuerySurface;
        var entityPath = path;

        if (surface?.ResponseType != null)
        {
            var dotIndex = path.IndexOf('.');
            var head = dotIndex < 0 ? path : path[..dotIndex];

            if (!surface.TryResolve(head, out var headField))
            {
                resolvable = false;
                return false;
            }

            // Computed scalar mappings have no real navigation backing.
            if (headField.ResponseProperty is null && headField.MappingKind == FieldMappingKind.Explicit)
            {
                resolvable = false;
                return false;
            }

            entityPath = dotIndex < 0
                ? headField.EntityProperty.Name
                : $"{headField.EntityProperty.Name}.{path[(dotIndex + 1)..]}";
        }

        if (!ReflectionCache.TryResolvePropertyChain(targetType, entityPath, out var chain)
            || chain.Count == 0)
        {
            resolvable = false;
            return false;
        }

        terminalType = chain[^1].PropertyType;
        return true;
    }
}
