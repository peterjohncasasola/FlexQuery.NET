using FlexQuery.NET.Caching;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Metadata;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.QuerySurface;
using FlexQuery.NET.Resolvers;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that explicitly selected navigation paths are authorized through the
/// include list — globally, for entity and DTO-aware queries, on every provider.
///
/// <para>Authorization is structural and separate from field resolution:</para>
/// <list type="bullet">
///   <item>A select node with nested children (e.g. <c>Orders(OrderId,Status)</c>) is a
///   navigation projection; its exact path must exist in the include list.</item>
///   <item>A dotted-path select (e.g. <c>Orders.OrderId</c>) references the navigation
///   chain <c>Orders</c>; the deepest navigation segment (excluding the trailing scalar
///   leaf) must exist in the include list.</item>
/// </list>
///
/// <para>Resolution is not authorization: a field that resolves through
/// <c>QuerySurface</c>, a same-name convention, or an explicit mapping still requires
/// the exact include path. Computed scalar mappings are exempt — their select nodes
/// carry no children, so they are never navigation projections.</para>
///
/// <para>Exact matching: a deeper include (<c>Orders.OrderItems</c>) does not authorize
/// a shallower navigation select (<c>Orders.OrderId</c>), and a shallower include
/// (<c>Orders</c>) does not authorize a deeper navigation select
/// (<c>Orders.OrderItems.OrderItemId</c>). Matching is case-insensitive, consistent
/// with field resolution.</para>
///
/// <para>Strict mode throws <see cref="Exceptions.QueryValidationException"/>; lenient
/// mode (<c>StrictFieldValidation = false</c>) removes the offending select nodes while
/// preserving valid scalar sibling selections.</para>
/// </summary>
internal sealed class NavigationProjectionRequiresIncludeValidationRule : IValidationRule
{
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Select is not { Count: > 0 })
            return;

        var strict = context.ExecutionOptions?.StrictFieldValidation ?? true;
        var includePaths = BuildIncludePathSet(options.Includes);

        for (var i = options.Select.Count - 1; i >= 0; i--)
        {
            var node = options.Select[i];

            // A) Dotted-path navigation reference (e.g. Orders.OrderId): the required
            //    include path is the deepest navigation segment — the trailing scalar
            //    leaf is excluded. Wildcard/default-projection expansion nodes are
            //    exempt: they are library-generated, part of the established default
            //    and wildcard projection contract.
            if (node.Children.Count == 0
                && !node.IsSynthesized
                && node.Field is not null
                && node.Field.Contains('.')
                && TryGetRequiredNavigationPath(node.Field, context, out var requiredPath)
                && !includePaths.Contains(requiredPath))
            {
                if (strict)
                {
                    result.Errors.Add(new ValidationError(
                        $"The navigation path '{requiredPath}' is referenced in the select clause but is not included. Add include={requiredPath}.",
                        ValidationErrorCodes.NavigationProjectionRequiresInclude,
                        requiredPath));
                }
                else
                {
                    options.Select.RemoveAt(i);
                }

                continue;
            }

            // B) Nested navigation projection (e.g. Orders(OrderId,Status)): the node's
            //    exact path must be included; each nested level is validated recursively.
            if (!ValidateNode(node, string.Empty, includePaths, result, strict))
            {
                options.Select.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Validates one select node and its subtree. Returns false when the node itself is
    /// an unauthorized navigation projection and must be removed (lenient mode).
    /// </summary>
    private static bool ValidateNode(
        SelectNode node,
        string parentPath,
        HashSet<string> includePaths,
        ValidationResult result,
        bool strict)
    {
        if (node.Children.Count == 0)
            return true;

        var fullPath = string.IsNullOrEmpty(parentPath) ? node.Field : $"{parentPath}.{node.Field}";
        var authorized = includePaths.Contains(fullPath);

        if (!authorized)
        {
            if (strict)
            {
                result.Errors.Add(new ValidationError(
                    $"The navigation path '{fullPath}' is referenced in the select clause but is not included. Add include={fullPath}.",
                    ValidationErrorCodes.NavigationProjectionRequiresInclude,
                    fullPath));
            }
            else
            {
                // Lenient: drop the unauthorized navigation projection (children ride
                // along) while preserving valid sibling selections.
                return false;
            }
        }

        for (var i = node.Children.Count - 1; i >= 0; i--)
        {
            if (!ValidateNode(node.Children[i], fullPath, includePaths, result, strict))
            {
                node.Children.RemoveAt(i);
            }
        }

        return true;
    }

    /// <summary>
    /// Determines the exact include path required by a dotted-path select reference.
    /// The path is the full dotted path minus the trailing scalar leaf
    /// (<c>Orders.OrderItems.OrderItemId</c> requires <c>Orders.OrderItems</c>).
    /// Returns false when the reference is not a navigation selection (root scalar,
    /// computed scalar, or unresolvable path — handled by other rules).
    /// </summary>
    private static bool TryGetRequiredNavigationPath(
        string dottedPath,
        QueryContext context,
        out string requiredPath)
    {
        requiredPath = string.Empty;

        if (context.TargetType is null)
            return false;

        var segments = dottedPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2)
            return false;

        var surface = context.QuerySurface;

        if (surface?.ResponseType != null)
        {
            // DTO mode: the head segment is a public field; the remainder walks the
            // entity graph rooted at the head's entity property.
            var head = segments[0];

            if (!surface.TryResolve(head, out var resolved) || !resolved.IsNavigation)
                return false;

            // Computed scalar mappings have a placeholder entity property and no real
            // navigation backing — not navigation references.
            if (resolved.ResponseProperty is null)
                return false;

            var walkRoot = resolved.EntityProperty.PropertyType;
            var remainder = string.Join('.', segments.Skip(1));

            if (!ReflectionCache.TryResolvePropertyChain(walkRoot, remainder, out var chain) || chain.Count == 0)
                return false;

            var navCount = IsNavigationProperty(chain[^1].PropertyType) ? chain.Count : chain.Count - 1;
            if (navCount <= 0)
                return false;

            var parts = new List<string> { resolved.SurfaceName };
            for (var i = 0; i < navCount; i++)
                parts.Add(chain[i].Name);

            requiredPath = string.Join('.', parts);
            return true;
        }

        // Entity mode: the whole path is entity-level.
        var entityType = context.TargetType;

        if (!ReflectionCache.TryResolvePropertyChain(entityType, dottedPath, out var entityChain) || entityChain.Count == 0)
            return false;

        var entityNavCount = IsNavigationProperty(entityChain[^1].PropertyType) ? entityChain.Count : entityChain.Count - 1;
        if (entityNavCount <= 0)
            return false;

        var entityParts = new List<string>();
        for (var i = 0; i < entityNavCount; i++)
            entityParts.Add(entityChain[i].Name);

        requiredPath = string.Join('.', entityParts);
        return true;
    }

    private static bool IsNavigationProperty(Type propertyType)
    {
        if (SafePropertyResolver.TryGetCollectionElementType(propertyType, out _))
            return true;

        return propertyType.IsClass
               && propertyType != typeof(string)
               && !TypeClassification.IsScalarType(propertyType);
    }

    /// <summary>
    /// Builds the exact include path set: each include entry authorizes exactly its own
    /// path (trimmed, case-insensitive). A deeper entry does not authorize its parent,
    /// and a shallower entry does not authorize its children.
    /// </summary>
    private static HashSet<string> BuildIncludePathSet(List<string>? includes)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (includes is null)
            return paths;

        foreach (var include in includes)
        {
            if (string.IsNullOrWhiteSpace(include))
                continue;

            paths.Add(include.Trim());
        }

        return paths;
    }
}
