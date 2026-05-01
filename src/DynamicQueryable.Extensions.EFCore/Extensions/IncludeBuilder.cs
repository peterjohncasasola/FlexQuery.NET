using System.Linq.Expressions;
using System.Reflection;
using DynamicQueryable.Builders;
using DynamicQueryable.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace DynamicQueryable.Extensions.EFCore;

/// <summary>
/// Translates a list of <see cref="IncludeNode"/> trees into EF Core
/// <c>Include</c> / <c>ThenInclude</c> calls, optionally with inline
/// <c>Where</c> filtering on each navigation collection.
///
/// <para>
/// This is the <b>Include Pipeline</b> — it is completely independent of
/// <see cref="ExpressionBuilder"/> (the WHERE pipeline) and must not be
/// coupled to it. Consistency between the two pipelines is the caller's
/// responsibility (typically by authoring consistent <c>query=</c> and
/// <c>include=</c> parameters).
/// </para>
///
/// <para><b>EF Core translation produced (example):</b></para>
/// <code>
/// query
///   .Include(c => c.Orders.Where(o => o.Status == "Cancelled"))
///   .ThenInclude(o => o.OrderItems.Where(oi => oi.Id == 101));
/// </code>
/// </summary>
public static class IncludeBuilder
{
    // ── Public entry point ───────────────────────────────────────────────

    /// <summary>
    /// Applies all <see cref="IncludeNode"/> trees in
    /// <paramref name="includes"/> to <paramref name="query"/>.
    /// Returns <paramref name="query"/> unchanged when the list is empty.
    /// </summary>
    public static IQueryable<T> Apply<T>(
        IQueryable<T> query,
        IEnumerable<IncludeNode> includes)
        where T : class
    {
        foreach (var root in includes)
            query = ApplyNode(query, root);

        return query;
    }

    // ── Node recursion ───────────────────────────────────────────────────

    /// <summary>
    /// Applies a single root <see cref="IncludeNode"/> and recurses into
    /// its children via <see cref="ApplyChildNode{T,TPrev}"/>.
    /// </summary>
    private static IQueryable<T> ApplyNode<T>(IQueryable<T> query, IncludeNode node)
        where T : class
    {
        // Reflect the navigation property on the root type.
        var navProp = typeof(T).GetProperty(
            node.Path,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (navProp is null) return query; // unknown navigation — skip gracefully

        var navType = navProp.PropertyType;

        // ── Collection navigation ────────────────────────────────────────
        if (TryGetCollectionElementType(navType, out var elementType))
        {
            // Build:  root => root.Collection  (unfiltered)
            // Or:     root => root.Collection.Where(e => <filter>)
            var rootParam  = Expression.Parameter(typeof(T), "x");
            var collAccess = Expression.Property(rootParam, navProp);

            Expression navBody = collAccess;

            if (node.Filter is not null)
            {
                // Build predicate for the element type, then wrap with Where().
                var predicate = ExpressionBuilder.BuildPredicate(elementType, node.Filter);
                if (predicate is not null)
                    navBody = BuildWhereCall(collAccess, navType, elementType, predicate);
            }

            // selector : Func<T, IEnumerable<TElement>>
            var iEnumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
            var selectorType    = typeof(Func<,>).MakeGenericType(typeof(T), iEnumerableType);
            var selector        = Expression.Lambda(selectorType, navBody, rootParam);

            // query.Include(selector)
            var includeMethod = GetIncludeCollectionMethod<T>(elementType);
            var included = includeMethod.Invoke(null, [query, selector])!;

            // Recurse into children via ThenInclude.
            foreach (var child in node.Children)
                included = InvokeApplyChildNode(included, typeof(T), elementType, child);

            return (IQueryable<T>)included;
        }

        // ── Reference navigation ─────────────────────────────────────────
        {
            var rootParam  = Expression.Parameter(typeof(T), "x");
            var navAccess  = Expression.Property(rootParam, navProp);
            var selectorType = typeof(Func<,>).MakeGenericType(typeof(T), navType);
            var selector     = Expression.Lambda(selectorType, navAccess, rootParam);

            var includeMethod = GetIncludeReferenceMethod<T>(navType);
            var included = includeMethod.Invoke(null, [query, selector])!;

            foreach (var child in node.Children)
                included = InvokeApplyChildReferenceNode(included, typeof(T), navType, child);

            return (IQueryable<T>)included;
        }
    }

    /// <summary>
    /// Recurses into a child node after a <b>collection</b> Include,
    /// applying <c>ThenInclude</c>.
    /// </summary>
    private static object ApplyChildNode<T, TPrev>(
        object source, // IIncludableQueryable<T, IEnumerable<TPrev>>
        Type prevElementType,
        IncludeNode node)
        where T : class
    {
        var navProp = prevElementType.GetProperty(
            node.Path,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (navProp is null)
            return source; // skip

        var navType = navProp.PropertyType;

        if (TryGetCollectionElementType(navType, out var elementType))
        {
            // ThenInclude after collection: (TPrev) => TPrev.ChildCollection[.Where(...)]
            var prevParam  = Expression.Parameter(prevElementType, "p");
            var collAccess = Expression.Property(prevParam, navProp);

            Expression navBody = collAccess;

            if (node.Filter is not null)
            {
                var predicate = ExpressionBuilder.BuildPredicate(elementType, node.Filter);
                if (predicate is not null)
                    navBody = BuildWhereCall(collAccess, navType, elementType, predicate);
            }

            var iEnumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
            var selectorType    = typeof(Func<,>).MakeGenericType(prevElementType, iEnumerableType);
            var selector        = Expression.Lambda(selectorType, navBody, prevParam);

            var thenIncludeMethod = GetThenIncludeAfterCollectionMethod<T, TPrev>(elementType);
            var next = thenIncludeMethod.Invoke(null, [source, selector])!;

            foreach (var child in node.Children)
                next = InvokeApplyChildNode(next, typeof(T), elementType, child);

            return next;
        }

        // Reference ThenInclude
        {
            var prevParam  = Expression.Parameter(prevElementType, "p");
            var navAccess  = Expression.Property(prevParam, navProp);
            var selectorType = typeof(Func<,>).MakeGenericType(prevElementType, navType);
            var selector     = Expression.Lambda(selectorType, navAccess, prevParam);

            var thenIncludeMethod = GetThenIncludeAfterCollectionRefMethod<T, TPrev>(navType);
            var next = thenIncludeMethod.Invoke(null, [source, selector])!;

            foreach (var child in node.Children)
                next = InvokeApplyChildReferenceNode(next, typeof(T), navType, child);

            return next;
        }
    }

    /// <summary>
    /// Recurses into a child node after a <b>reference</b> Include.
    /// </summary>
    private static object ApplyChildReferenceNode<T, TPrev>(
        object source, // IIncludableQueryable<T, TPrev>
        Type prevType,
        IncludeNode node)
        where T : class
    {
        var navProp = prevType.GetProperty(
            node.Path,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (navProp is null) return source;

        var navType = navProp.PropertyType;

        var prevParam  = Expression.Parameter(prevType, "p");
        var navAccess  = Expression.Property(prevParam, navProp);
        var selectorType = typeof(Func<,>).MakeGenericType(prevType, navType);
        var selector     = Expression.Lambda(selectorType, navAccess, prevParam);

        var thenIncludeMethod = GetThenIncludeAfterReferenceMethod<T, TPrev>(navType);
        var next = thenIncludeMethod.Invoke(null, [source, selector])!;

        if (TryGetCollectionElementType(navType, out var elementType))
        {
            foreach (var child in node.Children)
                next = InvokeApplyChildNode(next, typeof(T), elementType, child);
        }
        else
        {
            foreach (var child in node.Children)
                next = InvokeApplyChildReferenceNode(next, typeof(T), navType, child);
        }

        return next;
    }

    // ── Reflection Invokers ──────────────────────────────────────────────

    private static readonly MethodInfo _applyChildNodeMethod = typeof(IncludeBuilder)
        .GetMethod(nameof(ApplyChildNode), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo _applyChildRefNodeMethod = typeof(IncludeBuilder)
        .GetMethod(nameof(ApplyChildReferenceNode), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static object InvokeApplyChildNode(object source, Type rootType, Type prevElementType, IncludeNode node)
    {
        return _applyChildNodeMethod
            .MakeGenericMethod(rootType, prevElementType)
            .Invoke(null, [source, prevElementType, node])!;
    }

    private static object InvokeApplyChildReferenceNode(object source, Type rootType, Type prevType, IncludeNode node)
    {
        return _applyChildRefNodeMethod
            .MakeGenericMethod(rootType, prevType)
            .Invoke(null, [source, prevType, node])!;
    }

    // ── Where() helper ───────────────────────────────────────────────────

    /// <summary>
    /// Builds <c>collection.Where(predicate)</c> as an expression node
    /// so it can be embedded inside a selector lambda (EF Core supports
    /// filtered includes via this pattern).
    /// </summary>
    private static MethodCallExpression BuildWhereCall(
        Expression collectionAccess,
        Type collectionType,
        Type elementType,
        LambdaExpression predicate)
    {
        var whereMethod = typeof(Enumerable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == nameof(Enumerable.Where) && m.GetParameters().Length == 2)
            .MakeGenericMethod(elementType);

        return Expression.Call(whereMethod, collectionAccess, predicate);
    }

    // ── Reflection helpers for EF Core Include / ThenInclude ─────────────

    private static MethodInfo GetIncludeCollectionMethod<T>(Type elementType) where T : class
    {
        // EntityFrameworkQueryableExtensions.Include<TEntity, TRelated>(
        //   IQueryable<TEntity>, Expression<Func<TEntity, IEnumerable<TRelated>>>)
        return typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m =>
            {
                if (m.Name != nameof(EntityFrameworkQueryableExtensions.Include)) return false;
                var ps = m.GetParameters();
                if (ps.Length != 2) return false;
                // second param should be Expression<Func<TEntity, IEnumerable<TRelated>>>
                return ps[1].ParameterType.IsGenericType
                    && ps[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>);
            })
            .MakeGenericMethod(typeof(T), typeof(IEnumerable<>).MakeGenericType(elementType));
    }

    private static MethodInfo GetIncludeReferenceMethod<T>(Type navType) where T : class
    {
        return typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m =>
            {
                if (m.Name != nameof(EntityFrameworkQueryableExtensions.Include)) return false;
                var ps = m.GetParameters();
                if (ps.Length != 2) return false;
                return ps[1].ParameterType.IsGenericType
                    && ps[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>);
            })
            .MakeGenericMethod(typeof(T), navType);
    }

    private static MethodInfo GetThenIncludeAfterCollectionMethod<T, TPrev>(Type elementType)
        where T : class
    {
        // ThenInclude<TEntity, TPreviousProperty, TProperty>(
        //   IIncludableQueryable<TEntity, IEnumerable<TPreviousProperty>>, ...)
        return typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m =>
            {
                if (m.Name != nameof(EntityFrameworkQueryableExtensions.ThenInclude)) return false;
                var ps = m.GetParameters();
                if (ps.Length != 2) return false;
                var firstParamType = ps[0].ParameterType;
                if (!firstParamType.IsGenericType) return false;
                var args = firstParamType.GetGenericArguments();
                // IIncludableQueryable<TEntity, IEnumerable<T>>
                return args.Length == 2
                    && args[1].IsGenericType
                    && args[1].GetGenericTypeDefinition() == typeof(IEnumerable<>);
            })
            .MakeGenericMethod(typeof(T), typeof(TPrev), typeof(IEnumerable<>).MakeGenericType(elementType));
    }

    private static MethodInfo GetThenIncludeAfterCollectionRefMethod<T, TPrev>(Type navType)
        where T : class
    {
        return typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m =>
            {
                if (m.Name != nameof(EntityFrameworkQueryableExtensions.ThenInclude)) return false;
                var ps = m.GetParameters();
                if (ps.Length != 2) return false;
                var firstParamType = ps[0].ParameterType;
                if (!firstParamType.IsGenericType) return false;
                var args = firstParamType.GetGenericArguments();
                return args.Length == 2
                    && args[1].IsGenericType
                    && args[1].GetGenericTypeDefinition() == typeof(IEnumerable<>);
            })
            .MakeGenericMethod(typeof(T), typeof(TPrev), navType);
    }

    private static MethodInfo GetThenIncludeAfterReferenceMethod<T, TPrev>(Type navType)
        where T : class
    {
        // ThenInclude<TEntity, TPreviousProperty, TProperty>(
        //   IIncludableQueryable<TEntity, TPreviousProperty>, ...)
        return typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m =>
            {
                if (m.Name != nameof(EntityFrameworkQueryableExtensions.ThenInclude)) return false;
                var ps = m.GetParameters();
                if (ps.Length != 2) return false;
                var firstParamType = ps[0].ParameterType;
                if (!firstParamType.IsGenericType) return false;
                var args = firstParamType.GetGenericArguments();
                // IIncludableQueryable<TEntity, TPrev>  — NOT IEnumerable<>
                return args.Length == 2
                    && !(args[1].IsGenericType
                         && args[1].GetGenericTypeDefinition() == typeof(IEnumerable<>));
            })
            .MakeGenericMethod(typeof(T), typeof(TPrev), navType);
    }

    // ── Private helper ────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when <paramref name="type"/> is a generic collection
    /// (i.e. implements <c>IEnumerable&lt;T&gt;</c>) and sets
    /// <paramref name="elementType"/> to the element type.
    /// Mirrors the internal <c>SafePropertyResolver.TryGetCollectionElementType</c>.
    /// </summary>
    private static bool TryGetCollectionElementType(Type type, out Type elementType)
    {
        elementType = null!;
        if (type == typeof(string)) return false;

        var enumerable = type.GetInterfaces()
            .Concat([type])
            .FirstOrDefault(i => i.IsGenericType
                              && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerable is null) return false;

        elementType = enumerable.GetGenericArguments()[0];
        return true;
    }
}
