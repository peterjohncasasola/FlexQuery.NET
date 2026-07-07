using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.EntityFrameworkCore.Includes;

/// <summary>
/// Translates a list of <see cref="IncludeNode"/> trees into EF Core
/// <c>Include</c> / <c>ThenInclude</c> calls, optionally with inline
/// <c>Where</c> filtering on each navigation collection.
///
/// <para>
/// This is the <b>Include Pipeline</b> — it is completely independent of
/// <see cref="Expressions.ExpressionBuilder"/> (the WHERE
/// pipeline) and must not be coupled to it. Consistency between the two
/// pipelines is the caller's responsibility (typically by authoring
/// consistent <c>query=</c> and <c>include=</c> parameters).
/// </para>
///
/// <para><b>EF Core translation produced (example):</b></para>
/// <code>
/// query
///   .Include(c => c.Orders.Where(o => o.Status == "Cancelled"))
///   .ThenInclude(o => o.OrderItems.Where(oi => oi.Id == 101));
/// </code>
///
/// <para>
/// <b>Behaviour note (carried over unchanged from the previous implementation):</b>
/// a collection navigation reached through a <i>reference</i> parent (e.g.
/// <c>customer.PrimaryAddress.Orders</c>) is never filtered, even if the node
/// has a filter — only collections reached directly from the root, or from
/// another collection, support filtering. This falls out of
/// <see cref="IncludeSelectorFactory.Build"/>'s <c>allowFilteredCollection</c>
/// parameter, which is <c>false</c> for <see cref="IncludeContext.AfterReference"/>.
/// </para>
/// </summary>
internal static class IncludeBuilder
{
    /// <summary>
    /// Applies all <see cref="IncludeNode"/> trees in <paramref name="options"/>
    /// to <paramref name="query"/>. Returns <paramref name="query"/> unchanged
    /// when the list is empty.
    /// </summary>
    public static IQueryable<T> Apply<T>(IQueryable<T> query, QueryOptions options)
        where T : class
    {
        if (options.Expand is null) return query;

        foreach (var root in options.Expand)
            query = (IQueryable<T>)ApplyNode(query, typeof(T), typeof(T), IncludeContext.Root, root, options);

        return query;
    }

    /// <summary>
    /// Applies a single <see cref="IncludeNode"/> — as the root <c>Include</c>,
    /// or as a <c>ThenInclude</c> reached via a collection or reference parent
    /// — then recurses into its children.
    /// 
    /// <para>
    /// Replaces what used to be three near-identical methods (root / after-collection
    /// / after-reference), each hardcoded to a different EF overload family plus a
    /// pair of reflection-invoker helpers to call across them. Everything here works
    /// off runtime <see cref="Type"/> values instead of compile-time generic
    /// parameters, so one recursive method now covers all three cases.
    /// </para>
    /// </summary>
    /// <param name="source">
    /// The <c>IQueryable&lt;TRoot&gt;</c> (for <see cref="IncludeContext.Root"/>) or the
    /// <c>IIncludableQueryable&lt;...&gt;</c> produced by the previous step — typed as
    /// <see cref="object"/> since its concrete generic arguments are only known at runtime here.
    /// </param>
    /// <param name="rootType">The root entity type, <c>T</c> in <see cref="Apply{T}"/>.</param>
    /// <param name="contextType">
    /// The type <paramref name="node"/>'s path is resolved against: <paramref name="rootType"/>
    /// at the root, or the previous step's navigation target type for children.
    /// </param>
    /// <param name="context"></param>
    /// <param name="node"></param>
    /// <param name="options"></param>
    private static object ApplyNode(
        object source,
        Type rootType,
        Type contextType,
        IncludeContext context,
        IncludeNode node,
        QueryOptions options)
    {
        var navigation = IncludeNavigationResolver.Resolve(contextType, node.Path);
        if (navigation is null) return source; // unknown navigation — skip gracefully

        var allowFilteredCollection = context != IncludeContext.AfterReference;
        var selector = IncludeSelectorFactory.Build(contextType, navigation, node.Filter, options, allowFilteredCollection);
        var propertyType = navigation.GetPropertyTypeForSelector(allowFilteredCollection);

        var method = IncludeMethodCache.Resolve(context, rootType, contextType, propertyType);
        var result = method.Invoke(null, [source, selector])!;

        var nextContext = navigation.IsCollection ? IncludeContext.AfterCollection : IncludeContext.AfterReference;

        foreach (var child in node.Children)
            result = ApplyNode(result, rootType, navigation.TargetType, nextContext, child, options);

        return result;
    }
}