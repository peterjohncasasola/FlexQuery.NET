using System.Collections.Concurrent;
using System.Reflection;

namespace FlexQuery.NET.Expressions;

internal static class ExpressionMethodCache
{
    private static readonly ConcurrentDictionary<(string Name, Type ElementType), MethodInfo> EnumerableMethods = new();

    public static MethodInfo EnumerableAnyWithPredicate(Type elementType)
        => GetEnumerable(nameof(Enumerable.Any), elementType, parameterCount: 2);

    public static MethodInfo EnumerableAll(Type elementType)
        => GetEnumerable(nameof(Enumerable.All), elementType, parameterCount: 2);

    public static MethodInfo EnumerableCount(Type elementType)
        => GetEnumerable(nameof(Enumerable.Count), elementType, parameterCount: 1);

    private static MethodInfo GetEnumerable(string name, Type elementType, int parameterCount)
        => EnumerableMethods.GetOrAdd((name + ":" + parameterCount, elementType), key =>
            typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == name
                            && m.IsGenericMethodDefinition
                            && m.GetParameters().Length == parameterCount)
                .MakeGenericMethod(key.ElementType));
}
