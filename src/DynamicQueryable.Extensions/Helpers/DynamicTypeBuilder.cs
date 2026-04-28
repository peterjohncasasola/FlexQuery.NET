using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Concurrent;

namespace DynamicQueryable.Helpers;

/// <summary>
/// Builds runtime types dynamically to be used in MemberInitExpressions.
/// Types are strongly cached to avoid memory leaks and maximize performance.
/// </summary>
public static class DynamicTypeBuilder
{
    private static readonly ModuleBuilder _moduleBuilder;
    private static readonly ConcurrentDictionary<string, Lazy<Type>> _builtTypes = new();

    static DynamicTypeBuilder()
    {
        var assemblyName = new AssemblyName("DynamicQueryable.DynamicTypes");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        _moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
    }

    /// <summary>
    /// Gets or creates a runtime type containing the specified properties.
    /// </summary>
    public static Type GetDynamicType(Dictionary<string, Type> properties)
    {
        // Unique cache key based on property names and their type hashes
        var key = string.Join("|", properties.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value.GetHashCode()}"));
        
        return _builtTypes.GetOrAdd(key, k => new Lazy<Type>(() =>
        {
            var typeName = "DynamicType_" + Guid.NewGuid().ToString("N");
            var typeBuilder = _moduleBuilder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);
            
            // EF Core requires a public parameterless constructor
            typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);

            foreach (var prop in properties)
            {
                var fieldBuilder = typeBuilder.DefineField("_" + prop.Key, prop.Value, FieldAttributes.Private);
                var propertyBuilder = typeBuilder.DefineProperty(prop.Key, PropertyAttributes.HasDefault, prop.Value, null);

                // Get method
                var getMethod = typeBuilder.DefineMethod("get_" + prop.Key, 
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, 
                    prop.Value, Type.EmptyTypes);
                var getIl = getMethod.GetILGenerator();
                getIl.Emit(OpCodes.Ldarg_0);
                getIl.Emit(OpCodes.Ldfld, fieldBuilder);
                getIl.Emit(OpCodes.Ret);

                // Set method
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
        })).Value;
    }
}
