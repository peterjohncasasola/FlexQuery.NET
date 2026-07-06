using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Concurrent;
using FlexQuery.NET.Caching;

namespace FlexQuery.NET.Helpers;

/// <summary>
/// Builds runtime types dynamically to be used in MemberInitExpressions.
/// Types are cached with bounded FIFO eviction to prevent unbounded memory growth.
/// </summary>
internal static class DynamicTypeBuilder
{
    private static readonly ModuleBuilder _moduleBuilder;
    private static readonly ConcurrentDictionary<string, Lazy<Type>> _builtTypes = new();
    private static readonly ConcurrentQueue<string> _insertionOrder = new();

    static DynamicTypeBuilder()
    {
        var assemblyName = new AssemblyName("FlexQuery.NET.DynamicTypes");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        _moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
    }

    /// <summary>
    /// Gets or creates a runtime type containing the specified properties.
    /// Uses a bounded cache (MaxCacheSize) with FIFO eviction to prevent
    /// unbounded memory growth in long-running applications.
    /// </summary>
    /// <param name="properties">A dictionary mapping property names to their CLR types.</param>
    /// <returns>A dynamically created type with the specified properties.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="properties"/> is null.</exception>
    public static Type GetDynamicType(Dictionary<string, Type> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        var key = BuildCacheKey(properties);
        
        if (_builtTypes.TryGetValue(key, out var existing))
        {
            return existing.Value;
        }

        var newType = new Lazy<Type>(() => CreateDynamicType(properties), LazyThreadSafetyMode.ExecutionAndPublication);

        if (_builtTypes.TryAdd(key, newType))
        {
            _insertionOrder.Enqueue(key);
            Trim();
        }

        return _builtTypes[key].Value;
    }

    /// <summary>
    /// Clears all cached dynamic types.
    /// </summary>
    public static void Clear()
    {
        _builtTypes.Clear();
        while (_insertionOrder.TryDequeue(out _)) { }
    }

    /// <summary>
    /// Gets the number of cached dynamic types.
    /// </summary>
    public static int Count => _builtTypes.Count;

    private static string BuildCacheKey(Dictionary<string, Type> properties)
    {
        return string.Join("|",
            properties
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => $"{p.Key}:{p.Value.FullName}"));
    }

    private static Type CreateDynamicType(Dictionary<string, Type> properties)
    {
        var typeName = "DynamicType_" + Guid.NewGuid().ToString("N");
        var typeBuilder = _moduleBuilder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);

        typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);

        foreach (var prop in properties)
        {
            var fieldBuilder = typeBuilder.DefineField("_" + prop.Key, prop.Value, FieldAttributes.Private);
            var propertyBuilder = typeBuilder.DefineProperty(prop.Key, PropertyAttributes.HasDefault, prop.Value, null);

            var getMethod = typeBuilder.DefineMethod("get_" + prop.Key,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                prop.Value, Type.EmptyTypes);
            var getIl = getMethod.GetILGenerator();
            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldfld, fieldBuilder);
            getIl.Emit(OpCodes.Ret);

            var setMethod = typeBuilder.DefineMethod("set_" + prop.Key,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                null, [prop.Value]);
            var setIl = setMethod.GetILGenerator();
            setIl.Emit(OpCodes.Ldarg_0);
            setIl.Emit(OpCodes.Ldarg_1);
            setIl.Emit(OpCodes.Stfld, fieldBuilder);
            setIl.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getMethod);
            propertyBuilder.SetSetMethod(setMethod);
        }

        return typeBuilder.CreateType()!;
    }

    private static void Trim()
    {
        var max = Math.Max(1, FlexQueryCacheSettings.MaxCacheSize);
        while (_builtTypes.Count > max && _insertionOrder.TryDequeue(out var key))
        {
            _builtTypes.TryRemove(key, out _);
        }
    }
}
