using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.EntityFrameworkCore.Includes;

/// <summary>
/// Resolves and caches the generic <c>Include</c> / <c>ThenInclude</c> method
/// definitions from <see cref="EntityFrameworkQueryableExtensions"/>, and
/// closes them over concrete runtime types on demand.
/// </summary>
internal static class IncludeMethodCache
{
    private static readonly MethodInfo IncludeDefinition = typeof(EntityFrameworkQueryableExtensions)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.Include)
            && m.GetParameters().Length == 2
            && m.GetParameters()[1].ParameterType.IsGenericType
            && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>));

    /// <summary><c>ThenInclude</c> overload for when the previous step was a collection Include.</summary>
    private static readonly MethodInfo ThenIncludeAfterCollectionDefinition = typeof(EntityFrameworkQueryableExtensions)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .First(m => IsThenInclude(m) && PreviousPropertyIsEnumerable(m));

    /// <summary><c>ThenInclude</c> overload for when the previous step was a reference Include.</summary>
    private static readonly MethodInfo ThenIncludeAfterReferenceDefinition = typeof(EntityFrameworkQueryableExtensions)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .First(m => IsThenInclude(m) && !PreviousPropertyIsEnumerable(m));

    /// <summary>
    /// Closes the correct <c>Include</c> / <c>ThenInclude</c> definition for
    /// <paramref name="context"/> over the given runtime types.
    /// </summary>
    public static MethodInfo Resolve(IncludeContext context, Type rootType, Type contextType, Type propertyType) =>
        context switch
        {
            IncludeContext.Root => IncludeDefinition.MakeGenericMethod(rootType, propertyType),
            IncludeContext.AfterCollection => ThenIncludeAfterCollectionDefinition.MakeGenericMethod(rootType, contextType, propertyType),
            IncludeContext.AfterReference => ThenIncludeAfterReferenceDefinition.MakeGenericMethod(rootType, contextType, propertyType),
            _ => throw new ArgumentOutOfRangeException(nameof(context), context, message: null)
        };

    private static bool IsThenInclude(MethodInfo m)
    {
        if (m.Name != nameof(EntityFrameworkQueryableExtensions.ThenInclude)) return false;
        var parameters = m.GetParameters();
        return parameters is [{ ParameterType.IsGenericType: true }, _];
    }

    private static bool PreviousPropertyIsEnumerable(MethodInfo m)
    {
        var previousPropertyType = m.GetParameters()[0].ParameterType.GetGenericArguments()[1];
        return previousPropertyType.IsGenericType
            && previousPropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>);
    }
}