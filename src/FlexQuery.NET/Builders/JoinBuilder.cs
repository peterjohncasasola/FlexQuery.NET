using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Models;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Builders;

/// <summary>
/// Service that utilizes reflection-based expression trees to invoke the generic Queryable.Join method.
/// </summary>
public static class JoinBuilder
{
    private static readonly MethodInfo ApplyInnerJoinMethod = typeof(JoinBuilder).GetMethod(nameof(ApplyInnerJoinInternal), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ApplyLeftJoinMethod = typeof(JoinBuilder).GetMethod(nameof(ApplyLeftJoinInternal), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Applies a join operation dynamically to the query based on the specified join option.
    /// </summary>
    public static IQueryable<JoinResult<TOuter, TInner>> ApplyJoins<TOuter, TInner>(
        IQueryable<TOuter> outer,
        IQueryable<TInner> inner,
        JoinOption joinOption)
    {
        var outerParam = Expression.Parameter(typeof(TOuter), "outer");
        var innerParam = Expression.Parameter(typeof(TInner), "inner");

        // 1. Build Outer Key Selector
        if (!SafePropertyResolver.TryResolveChain(typeof(TOuter), joinOption.LeftKey, out var outerChain) || outerChain.Count == 0)
        {
            throw new ArgumentException($"Invalid left key path: {joinOption.LeftKey}");
        }

        Expression outerKeyExpr = outerParam;
        foreach (var prop in outerChain)
        {
            outerKeyExpr = Expression.Property(outerKeyExpr, prop);
        }

        // 2. Build Inner Key Selector
        if (!SafePropertyResolver.TryResolveChain(typeof(TInner), joinOption.RightKey, out var innerChain) || innerChain.Count == 0)
        {
            throw new ArgumentException($"Invalid right key path: {joinOption.RightKey}");
        }

        Expression innerKeyExpr = innerParam;
        foreach (var prop in innerChain)
        {
            innerKeyExpr = Expression.Property(innerKeyExpr, prop);
        }

        // 3. Align Types (e.g. int vs int?)
        if (outerKeyExpr.Type != innerKeyExpr.Type)
        {
            if (Nullable.GetUnderlyingType(outerKeyExpr.Type) == innerKeyExpr.Type)
            {
                innerKeyExpr = Expression.Convert(innerKeyExpr, outerKeyExpr.Type);
            }
            else if (Nullable.GetUnderlyingType(innerKeyExpr.Type) == outerKeyExpr.Type)
            {
                outerKeyExpr = Expression.Convert(outerKeyExpr, innerKeyExpr.Type);
            }
            else
            {
                innerKeyExpr = Expression.Convert(innerKeyExpr, outerKeyExpr.Type);
            }
        }

        var keyType = outerKeyExpr.Type;
        var outerKeySelector = Expression.Lambda(typeof(Func<,>).MakeGenericType(typeof(TOuter), keyType), outerKeyExpr, outerParam);
        var innerKeySelector = Expression.Lambda(typeof(Func<,>).MakeGenericType(typeof(TInner), keyType), innerKeyExpr, innerParam);

        // 4. Invoke appropriate join helper
        var method = joinOption.Type == JoinType.Left ? ApplyLeftJoinMethod : ApplyInnerJoinMethod;
        var genericMethod = method.MakeGenericMethod(typeof(TOuter), typeof(TInner), keyType);

        return (IQueryable<JoinResult<TOuter, TInner>>)genericMethod.Invoke(null, [outer, inner, outerKeySelector, innerKeySelector])!;
    }

    private static IQueryable<JoinResult<TOuter, TInner>> ApplyInnerJoinInternal<TOuter, TInner, TKey>(
        IQueryable<TOuter> outer,
        IQueryable<TInner> inner,
        Expression<Func<TOuter, TKey>> outerKeySelector,
        Expression<Func<TInner, TKey>> innerKeySelector)
    {
        return outer.Join(
            inner,
            outerKeySelector,
            innerKeySelector,
            (o, i) => new JoinResult<TOuter, TInner> { Left = o, Right = i }
        );
    }

    private static IQueryable<JoinResult<TOuter, TInner>> ApplyLeftJoinInternal<TOuter, TInner, TKey>(
        IQueryable<TOuter> outer,
        IQueryable<TInner> inner,
        Expression<Func<TOuter, TKey>> outerKeySelector,
        Expression<Func<TInner, TKey>> innerKeySelector)
    {
        return outer.GroupJoin(
                inner,
                outerKeySelector,
                innerKeySelector,
                (o, i) => new { Outer = o, InnerCollection = i }
            )
            .SelectMany(
                x => x.InnerCollection.DefaultIfEmpty(),
                (x, innerItem) => new JoinResult<TOuter, TInner> { Left = x.Outer, Right = innerItem! }
            );
    }
}
