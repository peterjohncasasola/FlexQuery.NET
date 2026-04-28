using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Concurrent;
using DynamicQueryable.Helpers;

namespace DynamicQueryable.Builders;

/// <summary>
/// Recursively constructs MemberInitExpressions for dynamic projection 
/// mapped to strongly-typed runtime classes, allowing full EF Core server-side translation.
/// </summary>
public static class ProjectionBuilder
{
    private static readonly ConcurrentDictionary<string, Expression> _cache = new();

    public static Expression<Func<T, object>> Build<T>(Dictionary<string, object> selectTree)
    {
        var cacheKey = typeof(T).FullName + ":" + GenerateCacheKey(selectTree);

        return (Expression<Func<T, object>>)_cache.GetOrAdd(cacheKey, _ =>
        {
            var param = Expression.Parameter(typeof(T), "x");
            var memberInit = BuildMemberInit(param, typeof(T), selectTree);
            var boxed = Expression.Convert(memberInit, typeof(object));
            return Expression.Lambda<Func<T, object>>(boxed, param);
        });
    }

    private static Expression BuildMemberInit(Expression source, Type sourceType, Dictionary<string, object> selectTree)
    {
        if (selectTree == null || selectTree.Count == 0)
        {
            selectTree = GetPrimitiveProperties(sourceType);
        }

        var propertiesToSelect = new Dictionary<string, (Type TargetType, Expression Assignment)>();

        foreach (var kvp in selectTree)
        {
            var propInfo = sourceType.GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (propInfo == null) continue; // Ignore invalid properties safely

            var propAccess = Expression.Property(source, propInfo);
            var childTree = kvp.Value as Dictionary<string, object>;

            if (childTree != null && childTree.Count > 0)
            {
                if (IsIEnumerable(propInfo.PropertyType, out var itemType))
                {
                    // Collection handling: source.Select(i => new DynamicType { ... }).ToList()
                    var itemParam = Expression.Parameter(itemType, "i");
                    var itemInit = BuildMemberInit(itemParam, itemType, childTree);
                    var selectLambda = Expression.Lambda(itemInit, itemParam);

                    var selectMethod = typeof(Enumerable).GetMethods()
                        .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
                        .MakeGenericMethod(itemType, itemInit.Type);
                    
                    var toListMethod = typeof(Enumerable).GetMethod("ToList")!
                        .MakeGenericMethod(itemInit.Type);

                    var selectCall = Expression.Call(null, selectMethod, propAccess, selectLambda);
                    var toListCall = Expression.Call(null, toListMethod, selectCall);

                    var targetListType = typeof(List<>).MakeGenericType(itemInit.Type);

                    // EF Core handles conditional null checks naturally, but we specify it for safety
                    var nullCheck = Expression.Equal(propAccess, Expression.Constant(null, propInfo.PropertyType));
                    var condition = Expression.Condition(
                        nullCheck, 
                        Expression.Constant(null, targetListType), 
                        toListCall, 
                        targetListType);

                    propertiesToSelect[propInfo.Name] = (targetListType, condition);
                }
                else
                {
                    // Nested Object handling
                    var nestedInit = BuildMemberInit(propAccess, propInfo.PropertyType, childTree);
                    var isNullable = !propInfo.PropertyType.IsValueType || Nullable.GetUnderlyingType(propInfo.PropertyType) != null;
                    
                    if (isNullable)
                    {
                        var nullCheck = Expression.Equal(propAccess, Expression.Constant(null, propInfo.PropertyType));
                        var condition = Expression.Condition(
                            nullCheck, 
                            Expression.Constant(null, nestedInit.Type), 
                            nestedInit, 
                            nestedInit.Type);
                        
                        propertiesToSelect[propInfo.Name] = (nestedInit.Type, condition);
                    }
                    else
                    {
                        propertiesToSelect[propInfo.Name] = (nestedInit.Type, nestedInit);
                    }
                }
            }
            else
            {
                // Leaf assignment
                propertiesToSelect[propInfo.Name] = (propInfo.PropertyType, propAccess);
            }
        }

        // If all fields were invalid, emit an empty dynamic type
        if (propertiesToSelect.Count == 0)
        {
            var emptyType = DynamicTypeBuilder.GetDynamicType(new Dictionary<string, Type>());
            return Expression.New(emptyType);
        }

        // Build runtime type matching selected properties
        var dynamicType = DynamicTypeBuilder.GetDynamicType(propertiesToSelect.ToDictionary(p => p.Key, p => p.Value.TargetType));
        var newExpr = Expression.New(dynamicType);
        
        var bindings = propertiesToSelect.Select(p => 
        {
            var targetProp = dynamicType.GetProperty(p.Key)!;
            return Expression.Bind(targetProp, p.Value.Assignment);
        });

        return Expression.MemberInit(newExpr, bindings);
    }

    private static bool IsIEnumerable(Type type, out Type itemType)
    {
        itemType = null!;
        if (type == typeof(string)) return false;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            itemType = type.GetGenericArguments()[0];
            return true;
        }

        var ienum = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (ienum != null)
        {
            itemType = ienum.GetGenericArguments()[0];
            return true;
        }

        return false;
    }

    private static Dictionary<string, object> GetPrimitiveProperties(Type type)
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var pt = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            if (pt.IsPrimitive || pt.IsEnum || pt == typeof(string) || pt == typeof(decimal) || pt == typeof(DateTime) || pt == typeof(DateTimeOffset) || pt == typeof(Guid) || pt == typeof(TimeSpan))
            {
                dict[prop.Name] = null!;
            }
        }
        return dict;
    }

    private static string GenerateCacheKey(Dictionary<string, object> tree)
    {
        if (tree == null || tree.Count == 0) return "*";
        var keys = tree.OrderBy(k => k.Key).Select(k => 
            k.Key + (k.Value is Dictionary<string, object> child ? $"({GenerateCacheKey(child)})" : "")
        );
        return string.Join(",", keys);
    }
}
