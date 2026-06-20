using System.Collections.Concurrent;
using System.Reflection;

namespace FlexQuery.NET.Expressions;

internal static class ExpressionMethodCache
{
    private static readonly ConcurrentDictionary<(string Name, Type ElementType), MethodInfo> EnumerableMethods = new();
    private static readonly ConcurrentDictionary<(string Name, Type SourceType, Type ResultType), MethodInfo> AggregateMethods = new();

    private static readonly MethodInfo QueryableGroupByMethod = FindQueryableMethod(
        nameof(Queryable.GroupBy), genericArgsCount: 2, parametersCount: 2);

    private static readonly MethodInfo QueryableSelectMethod = FindQueryableMethod(
        nameof(Queryable.Select), genericArgsCount: 2, parametersCount: 2,
        additionalFilter: m => GetFuncArity(m.GetParameters()[1].ParameterType) == 3);

    private static readonly MethodInfo QueryableWhereMethod = FindQueryableMethod(
        nameof(Queryable.Where), genericArgsCount: 1, parametersCount: 2,
        additionalFilter: m => GetFuncArity(m.GetParameters()[1].ParameterType) == 3);

    private static readonly MethodInfo QueryableOrderByMethod = FindQueryableMethod(
        nameof(Queryable.OrderBy), genericArgsCount: 2, parametersCount: 2);

    private static readonly MethodInfo QueryableOrderByDescendingMethod = FindQueryableMethod(
        nameof(Queryable.OrderByDescending), genericArgsCount: 2, parametersCount: 2);

    private static readonly MethodInfo QueryableThenByMethod = FindQueryableMethod(
        nameof(Queryable.ThenBy), genericArgsCount: 2, parametersCount: 2);

    private static readonly MethodInfo QueryableThenByDescendingMethod = FindQueryableMethod(
        nameof(Queryable.ThenByDescending), genericArgsCount: 2, parametersCount: 2);

    private static readonly MethodInfo QueryableAsQueryableMethod = FindQueryableMethod(
        nameof(Queryable.AsQueryable), genericArgsCount: 1, parametersCount: 1);

    private static readonly MethodInfo QueryableSelectSimpleMethod = FindQueryableMethod(
        nameof(Queryable.Select), genericArgsCount: 2, parametersCount: 2,
        additionalFilter: m => GetFuncArity(m.GetParameters()[1].ParameterType) == 2);

    private static readonly MethodInfo QueryableWhereSimpleMethod = FindQueryableMethod(
        nameof(Queryable.Where), genericArgsCount: 1, parametersCount: 2,
        additionalFilter: m => GetFuncArity(m.GetParameters()[1].ParameterType) == 2);

    private static readonly MethodInfo QueryableSelectManyMethod = FindQueryableMethod(
        nameof(Queryable.SelectMany), genericArgsCount: 2, parametersCount: 2);

    private static readonly MethodInfo QueryableSelectManyWithResultMethod = FindQueryableMethod(
        nameof(Queryable.SelectMany), genericArgsCount: 3, parametersCount: 3);

    private static readonly MethodInfo EnumerableToListMethod = FindEnumerableGenericDefinition(
        nameof(Enumerable.ToList), parameters: 1);

    /// <summary>
    /// Gets the generic <c>Enumerable.Any&lt;T&gt;(IEnumerable&lt;T&gt;, Func&lt;T, bool&gt;)</c> method
    /// bound to <paramref name="elementType"/>.
    /// </summary>
    public static MethodInfo EnumerableAnyWithPredicate(Type elementType)
        => GetEnumerable(nameof(Enumerable.Any), elementType, parameterCount: 2);

    /// <summary>
    /// Gets the generic <c>Enumerable.All&lt;T&gt;(IEnumerable&lt;T&gt;, Func&lt;T, bool&gt;)</c> method
    /// bound to <paramref name="elementType"/>.
    /// </summary>
    public static MethodInfo EnumerableAll(Type elementType)
        => GetEnumerable(nameof(Enumerable.All), elementType, parameterCount: 2);

    /// <summary>
    /// Gets the generic <c>Enumerable.Count&lt;T&gt;(IEnumerable&lt;T&gt;)</c> method
    /// bound to <paramref name="elementType"/>.
    /// </summary>
    public static MethodInfo EnumerableCount(Type elementType)
        => GetEnumerable(nameof(Enumerable.Count), elementType, parameterCount: 1);

    /// <summary>
    /// Gets the open generic definition of <c>Enumerable.ToList&lt;T&gt;(IEnumerable&lt;T&gt;)</c>.
    /// </summary>
    public static MethodInfo EnumerableToList() => EnumerableToListMethod;

    /// <summary>
    /// Gets <c>Enumerable.Min&lt;TSource, TResult&gt;(IEnumerable&lt;TSource&gt;, Func&lt;TSource, TResult&gt;)</c>
    /// bound to <paramref name="sourceType"/> and <paramref name="resultType"/>.
    /// </summary>
    public static MethodInfo EnumerableMinWithSelector(Type sourceType, Type resultType)
        => GetAggregate(nameof(Enumerable.Min), sourceType, resultType);

    /// <summary>
    /// Gets <c>Enumerable.Max&lt;TSource, TResult&gt;(IEnumerable&lt;TSource&gt;, Func&lt;TSource, TResult&gt;)</c>
    /// bound to <paramref name="sourceType"/> and <paramref name="resultType"/>.
    /// </summary>
    public static MethodInfo EnumerableMaxWithSelector(Type sourceType, Type resultType)
        => GetAggregate(nameof(Enumerable.Max), sourceType, resultType);

    /// <summary>
    /// Gets the <c>Enumerable.Sum&lt;TSource&gt;(IEnumerable&lt;TSource&gt;, Func&lt;TSource, TResult&gt;)</c>
    /// overload whose selector result exactly matches <paramref name="resultType"/> (e.g. <c>int</c>,
    /// <c>decimal?</c>), bound to <paramref name="sourceType"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No <c>Sum</c> overload accepts a selector returning exactly <paramref name="resultType"/>.
    /// </exception>
    public static MethodInfo EnumerableSumWithSelector(Type sourceType, Type resultType)
        => GetConcreteNumericAggregate(nameof(Enumerable.Sum), sourceType, resultType);

    /// <summary>
    /// Gets the <c>Enumerable.Average&lt;TSource&gt;(IEnumerable&lt;TSource&gt;, Func&lt;TSource, TResult&gt;)</c>
    /// overload whose selector result exactly matches <paramref name="resultType"/> (e.g. <c>int</c>,
    /// <c>decimal?</c>), bound to <paramref name="sourceType"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No <c>Average</c> overload accepts a selector returning exactly <paramref name="resultType"/>.
    /// </exception>
    public static MethodInfo EnumerableAverageWithSelector(Type sourceType, Type resultType)
        => GetConcreteNumericAggregate(nameof(Enumerable.Average), sourceType, resultType);

    /// <summary>
    /// Gets the open generic definition of
    /// <c>Queryable.GroupBy&lt;TSource, TKey&gt;(IQueryable&lt;TSource&gt;, Expression&lt;Func&lt;TSource, TKey&gt;&gt;)</c>.
    /// </summary>
    public static MethodInfo QueryableGroupBy() => QueryableGroupByMethod;

    /// <summary>
    /// Gets the open generic definition of the two-parameter (index-aware)
    /// <c>Queryable.Select&lt;TSource, TResult&gt;</c> overload.
    /// </summary>
    public static MethodInfo QueryableSelect() => QueryableSelectMethod;

    /// <summary>
    /// Gets the open generic definition of the simple (non-index)
    /// <c>Queryable.Select&lt;TSource, TResult&gt;</c> overload.
    /// </summary>
    public static MethodInfo QueryableSelectSimple() => QueryableSelectSimpleMethod;

    /// <summary>
    /// Gets the open generic definition of <c>Queryable.SelectMany&lt;TSource, TResult&gt;(IQueryable&lt;TSource&gt;,
    /// Expression&lt;Func&lt;TSource, IEnumerable&lt;TResult&gt;&gt;&gt;)</c> (2-parameter, no result selector).
    /// </summary>
    public static MethodInfo QueryableSelectMany() => QueryableSelectManyMethod;

    /// <summary>
    /// Gets the open generic definition of <c>Queryable.SelectMany&lt;TSource, TCollection, TResult&gt;</c>
    /// (3-parameter overload with a collection selector and a result selector).
    /// </summary>
    public static MethodInfo QueryableSelectManyWithResult() => QueryableSelectManyWithResultMethod;

    /// <summary>
    /// Gets the open generic definition of the two-parameter (index-aware)
    /// <c>Queryable.Where&lt;TSource&gt;</c> overload.
    /// </summary>
    public static MethodInfo QueryableWhere() => QueryableWhereMethod;

    /// <summary>
    /// Gets the open generic definition of the simple (non-index)
    /// <c>Queryable.Where&lt;TSource&gt;</c> overload.
    /// </summary>
    public static MethodInfo QueryableWhereSimple() => QueryableWhereSimpleMethod;

    /// <summary>
    /// Gets the open generic definition of <c>Queryable.OrderBy&lt;TSource, TKey&gt;</c>.
    /// </summary>
    public static MethodInfo QueryableOrderBy() => QueryableOrderByMethod;

    /// <summary>
    /// Gets the open generic definition of <c>Queryable.OrderByDescending&lt;TSource, TKey&gt;</c>.
    /// </summary>
    public static MethodInfo QueryableOrderByDescending() => QueryableOrderByDescendingMethod;

    /// <summary>
    /// Gets the open generic definition of <c>Queryable.ThenBy&lt;TSource, TKey&gt;</c>.
    /// </summary>
    public static MethodInfo QueryableThenBy() => QueryableThenByMethod;

    /// <summary>
    /// Gets the open generic definition of <c>Queryable.ThenByDescending&lt;TSource, TKey&gt;</c>.
    /// </summary>
    public static MethodInfo QueryableThenByDescending() => QueryableThenByDescendingMethod;

    /// <summary>
    /// Gets the open generic definition of <c>Queryable.AsQueryable&lt;TElement&gt;(IEnumerable&lt;TElement&gt;)</c>.
    /// </summary>
    public static MethodInfo QueryableAsQueryable() => QueryableAsQueryableMethod;

    private static MethodInfo GetEnumerable(string name, Type elementType, int parameterCount)
        => EnumerableMethods.GetOrAdd((name + ":" + parameterCount, elementType), key =>
            typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == name
                            && m.IsGenericMethodDefinition
                            && m.GetParameters().Length == parameterCount)
                .MakeGenericMethod(key.ElementType));

    /// <summary>
    /// Resolves and caches the generic <c>Min</c>/<c>Max</c> overload
    /// <c>(IEnumerable&lt;TSource&gt;, Func&lt;TSource, TResult&gt;)</c> bound to the given types.
    /// </summary>
    private static MethodInfo GetAggregate(string name, Type sourceType, Type resultType)
        => AggregateMethods.GetOrAdd((name, sourceType, resultType), key =>
            typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name.Equals(key.Name, StringComparison.OrdinalIgnoreCase)
                             && m.IsGenericMethodDefinition
                             && m.GetGenericArguments().Length == 2
                             && m.GetParameters().Length == 2)
                .MakeGenericMethod(key.SourceType, key.ResultType));

    /// <summary>
    /// Resolves and caches the concrete (non-generic-in-result) <c>Sum</c>/<c>Average</c> overload
    /// whose selector result type exactly matches <paramref name="resultType"/>, then binds it to
    /// <paramref name="sourceType"/>.
    /// </summary>
    private static MethodInfo GetConcreteNumericAggregate(string name, Type sourceType, Type resultType)
        => AggregateMethods.GetOrAdd((name, sourceType, resultType), key =>
        {
            var match = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name.Equals(key.Name, StringComparison.OrdinalIgnoreCase)
                            && m.IsGenericMethodDefinition
                            && m.GetGenericArguments().Length == 1
                            && m.GetParameters().Length == 2)
                .FirstOrDefault(m =>
                {
                    var selectorType = m.GetParameters()[1].ParameterType;
                    if (!selectorType.IsGenericType) return false;

                    var funcArgs = selectorType.GetGenericArguments();
                    return funcArgs.Length == 2 && funcArgs[1] == key.ResultType;
                });

            if (match is null)
            {
                throw new InvalidOperationException(
                    $"No Enumerable.{key.Name} overload accepts a selector returning '{key.ResultType}'.");
            }

            return match.MakeGenericMethod(key.SourceType);
        });

    private static MethodInfo FindQueryableMethod(
        string methodName,
        int genericArgsCount,
        int parametersCount,
        Func<MethodInfo, bool>? additionalFilter = null)
    {
        return typeof(Queryable).GetMethods()
            .First(m =>
                m.Name == methodName
                && m.IsGenericMethodDefinition
                && m.GetGenericArguments().Length == genericArgsCount
                && m.GetParameters().Length == parametersCount
                && (additionalFilter is null || additionalFilter(m)));
    }

    private static MethodInfo FindEnumerableGenericDefinition(string methodName, int parameters)
        => typeof(Enumerable).GetMethods()
            .Single(m => m.Name == methodName
                         && m.IsGenericMethodDefinition
                         && m.GetParameters().Length == parameters);

    private static int GetFuncArity(Type expressionType)
    {
        if (!expressionType.IsGenericType)
            return 0;

        var wrapped = expressionType.GetGenericArguments().FirstOrDefault();
        if (wrapped is null || !wrapped.IsGenericType)
            return 0;

        return wrapped.GetGenericArguments().Length;
    }
}