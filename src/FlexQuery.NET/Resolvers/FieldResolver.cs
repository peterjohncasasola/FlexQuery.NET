using System.Linq.Expressions;
using FlexQuery.NET.Caching;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.QuerySurface;
using FlexQuery.NET.Security;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.Resolvers;

/// <summary>
/// Single field-resolution entry point shared by every query operation
/// (filter, sort, group, aggregate, projection, keyset paging).
/// Resolution order: QuerySurface (DTO mode, authoritative) → ExpressionMappings →
/// reflection. When a DTO surface is active, reflection on the entity type is
/// suppressed so entity-only names cannot leak into the public query API.
/// </summary>
internal static class FieldResolver
{
    /// <summary>
    /// Resolves a (possibly dotted) field path to an entity-level expression rooted at
    /// <paramref name="current"/>. In DTO mode the first segment must resolve through the
    /// public <see cref="IQuerySurface"/>; the remainder resolves over the entity graph.
    /// </summary>
    public static bool TryResolveMappedExpression(
        Expression current,
        string path,
        QueryOptions options,
        out Expression resolvedExpression,
        out Type resolvedType)
    {
        resolvedExpression = null!;
        resolvedType = null!;

        if (string.IsNullOrWhiteSpace(path)) return false;

        var surface = GetSurface(options);

        // 1. DTO surface is the authoritative mapping source at the query root only.
        //    Scoped contexts (expand sort/selection, scoped collection filters) resolve
        //    element-level entity names on the navigation's target type, not root public
        //    surface names — rebinding a root lambda onto a foreign parameter type would
        //    produce an invalid expression tree.
        if (surface?.ResponseType != null
            && current.Type == surface.EntityType
            && TryResolveViaSurface(current, path, surface, out resolvedExpression, out resolvedType))
        {
            return true;
        }

        // 2. Legacy string-alias ExpressionMappings (non-DTO mode). The mapped lambda's
        //    parameter type must be compatible with the current expression.
        var mappings = GetMappings(options);
        if (mappings != null
            && mappings.TryGetValue(path, out var exactMappedLambda)
            && exactMappedLambda.Parameters[0].Type.IsAssignableFrom(current.Type))
        {
            resolvedExpression = ReplaceParameter(exactMappedLambda, current);
            resolvedType = exactMappedLambda.ReturnType;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Legacy type resolution used by callers without a <see cref="QueryContext"/>.
    /// Consults <paramref name="execOptions"/> expression mappings, then entity reflection.
    /// </summary>
    public static bool TryResolveType(
        Type entityType,
        string path,
        QueryGovernanceOptions? execOptions,
        out Type resolvedType)
    {
        resolvedType = null!;
        if (string.IsNullOrWhiteSpace(path)) return false;

        var mappings = execOptions?.ExpressionMappings;

        if (mappings != null && mappings.TryGetValue(path, out var exactMappedLambda))
        {
            resolvedType = exactMappedLambda.ReturnType;
            return true;
        }

        if (SafePropertyResolver.TryResolveChain(entityType, path, out var fullChain) && fullChain.Count > 0)
        {
            resolvedType = fullChain.Last().PropertyType;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Public-surface-aware type resolution for validation. In DTO mode the field must
    /// resolve through the surface's public names — entity-only names are rejected.
    /// </summary>
    public static bool TryResolvePublicType(
        IQuerySurface? surface,
        Type entityType,
        string path,
        QueryGovernanceOptions? execOptions,
        out Type resolvedType)
    {
        resolvedType = null!;
        if (string.IsNullOrWhiteSpace(path)) return false;

        if (surface?.ResponseType == null) return TryResolveType(entityType, path, execOptions, out resolvedType);
        
        if (surface.TryResolve(path, out var resolved))
        {
            resolvedType = resolved.EntityExpression.ReturnType;
            return true;
        }

        var dotIndex = path.IndexOf('.');
        if (dotIndex <= 0 || !surface.TryResolve(path[..dotIndex], out var head)) return false;

        if (!ReflectionCache.TryResolvePropertyChain(head.EntityProperty.PropertyType, path[(dotIndex + 1)..],
                out var chain)
            || chain.Count <= 0) 
            return false;
        
        resolvedType = chain[^1].PropertyType;
        return true;

    }

    /// <summary>Returns true when a DTO surface is active for this query.</summary>
    public static bool IsDtoSurfaceActive(QueryOptions options)
        => GetSurface(options)?.ResponseType != null;

    /// <summary>
    /// Translates a public include/expand path to its entity-level path: the head segment
    /// resolves through the public surface (mapped navigation), the remainder is already
    /// entity-level (element types have no surface). Returns the input unchanged when the
    /// head does not resolve — lenient leftovers are skipped downstream.
    /// </summary>
    private static string TranslatePathToEntity(IQuerySurface? surface, string path)
    {
        if (surface?.ResponseType == null || string.IsNullOrWhiteSpace(path))
            return path;

        var dotIndex = path.IndexOf('.');
        var head = dotIndex < 0 ? path : path[..dotIndex];

        if (!surface.TryResolve(head, out var headField))
            return path;

        var entityHead = headField.EntityProperty.Name;
        return dotIndex < 0 ? entityHead : $"{entityHead}.{path[(dotIndex + 1)..]}";
    }

    /// <summary>
    /// Rewrites include/expand paths from public (DTO) names to entity property names in
    /// place, after validation. Root-level segments only — child expand paths are
    /// element-level names on the navigation target and have no surface entries.
    /// </summary>
    public static void TranslateIncludePathsToEntity(QueryOptions options, IQuerySurface? surface)
    {
        if (surface?.ResponseType == null)
            return;

        if (options.Includes is { Count: > 0 })
        {
            for (var i = 0; i < options.Includes.Count; i++)
                options.Includes[i] = TranslatePathToEntity(surface, options.Includes[i]);
        }

        if (options.Expand is { Count: > 0 })
            TranslateExpandNodes(options.Expand, surface, rootLevel: true);
    }

    private static void TranslateExpandNodes(List<IncludeNode> nodes, IQuerySurface surface, bool rootLevel)
    {
        foreach (var node in nodes)
        {
            if (rootLevel)
                node.Path = TranslatePathToEntity(surface, node.Path);

            if (node.Children is { Count: > 0 })
                TranslateExpandNodes(node.Children, surface, rootLevel: false);
        }
    }

    /// <summary>
    /// Suppresses DTO-surface enforcement while descending into nested contexts
    /// (scoped collection filters, nested navigations) whose fields are entity-level,
    /// not part of the root public surface. Restore with the returned disposable.
    /// </summary>
    public static IDisposable SuppressSurface(QueryOptions options)
    {
        var hadSurface = options.Items.TryGetValue(ContextKeys.QuerySurface, out var existing);
        if (hadSurface)
            options.Items.Remove(ContextKeys.QuerySurface);

        return new SurfaceScope(options, hadSurface ? existing : null);
    }

    private sealed class SurfaceScope(QueryOptions options, object? surface) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed || surface is null) return;
            _disposed = true;
            options.Items[ContextKeys.QuerySurface] = surface;
        }
    }

    private static IQuerySurface? GetSurface(QueryOptions options)
        => options.Items.TryGetValue(ContextKeys.QuerySurface, out var surfaceObj) && surfaceObj is IQuerySurface surface
            ? surface
            : null;

    /// <summary>
    /// Resolves the path's first segment through the public surface (when a DTO surface
    /// is active) and any remaining segments over the entity graph.
    /// </summary>
    private static bool TryResolveViaSurface(
        Expression current,
        string path,
        IQuerySurface? surface,
        out Expression resolvedExpression,
        out Type resolvedType)
    {
        resolvedExpression = null!;
        resolvedType = null!;

        if (surface?.ResponseType == null) return false;

        if (surface.TryResolve(path, out var resolved))
        {
            resolvedExpression = ReplaceParameter(resolved.EntityExpression, current);
            resolvedType = resolved.EntityExpression.ReturnType;
            return true;
        }

        var dotIndex = path.IndexOf('.');
        if (dotIndex <= 0) return false;

        var head = path[..dotIndex];
        if (!surface.TryResolve(head, out var headField)) return false;

        var headExpression = ReplaceParameter(headField.EntityExpression, current);
        return TryChain(
            headExpression,
            headField.EntityProperty.PropertyType,
            path[(dotIndex + 1)..],
            out resolvedExpression,
            out resolvedType);
    }

    private static bool TryChain(Expression root, Type rootType, string path, out Expression resolvedExpression, out Type resolvedType)
    {
        resolvedExpression = null!;
        resolvedType = null!;

        if (!ReflectionCache.TryResolvePropertyChain(rootType, path, out var chain) || chain.Count == 0)
            return false;

        var access = root;
        foreach (var prop in chain)
            access = Expression.Property(access, prop);

        resolvedExpression = access;
        resolvedType = access.Type;
        return true;
    }

    private static IReadOnlyDictionary<string, LambdaExpression>? GetMappings(QueryOptions options)
    {
        if (options.Items.TryGetValue(ContextKeys.ExpressionMappings, out var mappingsObj) && mappingsObj is IReadOnlyDictionary<string, LambdaExpression> mappings)
        {
            return mappings;
        }
        return null;
    }

    private static Expression ReplaceParameter(LambdaExpression lambda, Expression replacement)
    {
        var map = new Dictionary<ParameterExpression, Expression>
        {
            { lambda.Parameters[0], replacement }
        };
        return ParameterRebinder.ReplaceParameters(map, lambda.Body);
    }
}
