using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Caching;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Expressions;
using FlexQuery.NET.Metadata;
using FlexQuery.NET.Resolvers;

namespace FlexQuery.NET.Builders;

internal static class FlatProjectionBuilder
{
    private static readonly MethodInfo QueryableSelectManyWithResult = ExpressionMethodCache.QueryableSelectManyWithResult();
    private static readonly MethodInfo QueryableSelectManySimple = ExpressionMethodCache.QueryableSelectMany();
    private static readonly MethodInfo SelectMethod = ExpressionMethodCache.QueryableSelectSimple();
    

    // ── Data structures ─────────────────────────────────────────────────

    /// <summary>A single nav hop in the SelectMany chain.</summary>
    private record NavHop(string PropName, Type SourceType, Type ElementType);

    /// <summary>
    /// A field to project in the final row.
    /// Level = index into the nav chain where this field lives.
    ///   -1 = root entity (Customer)
    ///    0 = first nav level (Order)
    ///    1 = second nav level (OrderItem)
    /// </summary>
    private record FieldSpec(int Level, string PropName, string OutputName, Type PropType);

    // ── Entry points ─────────────────────────────────────────────────────

    /// <summary>Flat mode: strict single linear path, leaf-only output rows.</summary>
    public static IQueryable<object> BuildAndApply<T>(IQueryable<T> query, SelectionNode tree, QueryOptions options)
    {
        var (hops, fields) = Decompose(tree, typeof(T), parentLevel: -1, allowRootScalars: false, options);
        return ApplyFlatChain(query, typeof(T), hops, fields, options);
    }

    /// <summary>FlatMixed mode: root + intermediate + leaf fields all in one flat row.</summary>
    public static IQueryable<object> BuildAndApplyMixed<T>(IQueryable<T> query, SelectionNode tree, QueryOptions options)
    {
        var (hops, fields) = Decompose(tree, typeof(T), parentLevel: -1, allowRootScalars: true, options);
        return ApplyFlatMixedChain(query, typeof(T), hops, fields, options);
    }

    // ── Flat (strict, leaf-only) ─────────────────────────────────────────

    private static IQueryable<object> ApplyFlatChain(
        IQueryable source,
        Type currentType,
        List<NavHop> hops,
        List<FieldSpec> fields,
        QueryOptions options)
    {
        // Walk the navigation path using single-arg SelectMany
        foreach (var hop in hops)
        {
            var prop = ReflectionCache.GetProperty(hop.SourceType, hop.PropName)!;
            var param = Expression.Parameter(hop.SourceType, "x");
            var propAccess = Expression.Property(param, prop);

            var enumType = typeof(IEnumerable<>).MakeGenericType(hop.ElementType);
            var funcType = typeof(Func<,>).MakeGenericType(hop.SourceType, enumType);
            var lambda = Expression.Lambda(funcType, propAccess, param);

            var method = QueryableSelectManySimple.MakeGenericMethod(hop.SourceType, hop.ElementType);
            source = (IQueryable)method.Invoke(null, [source, lambda])!;
            currentType = hop.ElementType;
        }

        // Project only leaf fields
        return ProjectToFlatRow(source, currentType, fields, options, levelAccessors: null);
    }

    // ── FlatMixed ────────────────────────────────────────────────────────

    private static IQueryable<object> ApplyFlatMixedChain(
        IQueryable source,
        Type rootType,
        List<NavHop> hops,
        List<FieldSpec> fields,
        QueryOptions options)
    {
        if (hops.Count == 0)
        {
            return ProjectToFlatRow(source, rootType, fields, options, levelAccessors: null);
        }

        IQueryable currentQuery = source;
        Type currentType = rootType;

        var levelTypes = new List<Type> { rootType };
        Type? contextType = null;

        for (int i = 0; i < hops.Count; i++)
        {
            var hop = hops[i];
            var prop = ReflectionCache.GetProperty(currentType, hop.PropName)!;

            if (i == 0)
            {
                var rootParam = Expression.Parameter(rootType, "root");
                var indexParam = Expression.Parameter(typeof(int), "idx");
                var itemParam = Expression.Parameter(hop.ElementType, "item0");

                var propAccess = Expression.Property(rootParam, prop);
                var enumType = typeof(IEnumerable<>).MakeGenericType(hop.ElementType);
                var collFuncType = typeof(Func<,>).MakeGenericType(rootType, enumType);
                var collSelector = Expression.Lambda(collFuncType, propAccess, rootParam);

                levelTypes.Add(hop.ElementType);
                var ctxFields = BuildLevelTypeFields(levelTypes);
                contextType = DynamicTypeBuilder.GetDynamicType(ctxFields);

                var ctxNew = Expression.New(contextType);
                var ctxBindings = new List<MemberBinding>
                {
                    Expression.Bind(contextType.GetProperty("L0")!, rootParam),
                    Expression.Bind(contextType.GetProperty("L1")!, itemParam)
                };
                var ctxInit = Expression.MemberInit(ctxNew, ctxBindings);
                // EF Core non-indexed result selector: Func<TSource, TCollection, TResult>
                var resFuncType = typeof(Func<,,>).MakeGenericType(rootType, hop.ElementType, contextType);
                var resSelector = Expression.Lambda(resFuncType, ctxInit, rootParam, itemParam);

                var method = QueryableSelectManyWithResult.MakeGenericMethod(rootType, hop.ElementType, contextType);
                currentQuery = (IQueryable)method.Invoke(null, [currentQuery, collSelector, resSelector])!;
                currentType = contextType;
            }
            else
            {
                var ctxParam = Expression.Parameter(currentType, $"ctx{i}");
                var indexParam = Expression.Parameter(typeof(int), "idx");
                var itemParam = Expression.Parameter(hop.ElementType, $"item{i}");

                var prevLevelProp = currentType.GetProperty($"L{i}")!;
                var prevLevelAccess = Expression.Property(ctxParam, prevLevelProp);
                var navProp = ReflectionCache.GetProperty(hop.SourceType, hop.PropName)!;
                var propAccess = Expression.Property(prevLevelAccess, navProp);

                var enumType = typeof(IEnumerable<>).MakeGenericType(hop.ElementType);
                var collFuncType = typeof(Func<,>).MakeGenericType(currentType, enumType);
                var collSelector = Expression.Lambda(collFuncType, propAccess, ctxParam);

                levelTypes.Add(hop.ElementType);
                var ctxFields = BuildLevelTypeFields(levelTypes);
                var newContextType = DynamicTypeBuilder.GetDynamicType(ctxFields);

                var ctxNew = Expression.New(newContextType);
                var ctxBindings = new List<MemberBinding>();
                for (int lvl = 0; lvl <= i; lvl++)
                {
                    var srcProp = currentType.GetProperty($"L{lvl}")!;
                    var dstProp = newContextType.GetProperty($"L{lvl}")!;
                    ctxBindings.Add(Expression.Bind(dstProp, Expression.Property(ctxParam, srcProp)));
                }
                ctxBindings.Add(Expression.Bind(newContextType.GetProperty($"L{i + 1}")!, itemParam));

                var ctxInit = Expression.MemberInit(ctxNew, ctxBindings);
                var resFuncType = typeof(Func<,,>).MakeGenericType(currentType, hop.ElementType, newContextType);
                var resSelector = Expression.Lambda(resFuncType, ctxInit, ctxParam, itemParam);

                var method = QueryableSelectManyWithResult.MakeGenericMethod(currentType, hop.ElementType, newContextType);
                currentQuery = (IQueryable)method.Invoke(null, [currentQuery, collSelector, resSelector])!;
                currentType = newContextType;
                contextType = newContextType;
            }
        }

        var ctxFinalParam = Expression.Parameter(currentType, "ctx");
        var levelAccessors = new Dictionary<int, Expression>();
        for (int lvl = 0; lvl < levelTypes.Count; lvl++)
        {
            var p = currentType.GetProperty($"L{lvl}");
            if (p != null)
                levelAccessors[lvl - 1] = Expression.Property(ctxFinalParam, p); // root=-1 → L0
        }

        return ProjectToFlatRow(currentQuery, currentType, fields, options, levelAccessors, ctxFinalParam);
    }

    // ── Final row projection ─────────────────────────────────────────────

    /// <summary>
    /// Projects into the final flat output row.
    /// When levelAccessors is null, <paramref name="source"/> elements ARE the leaf objects directly.
    /// When levelAccessors is set, each field is reached via its level accessor.
    /// </summary>
    private static IQueryable<object> ProjectToFlatRow(
        IQueryable source,
        Type sourceType,
        List<FieldSpec> fields,
        QueryOptions options,
        Dictionary<int, Expression>? levelAccessors,
        ParameterExpression? ctxParam = null)
    {
        var param = ctxParam ?? Expression.Parameter(sourceType, "x");
        var props = new Dictionary<string, (Type Type, Expression Expr)>();

        foreach (var field in fields)
        {
            Expression? ownerAccess;

            if (levelAccessors == null)
            {
                // Leaf-only flat mode: source IS the leaf entity
                ownerAccess = param;
            }
            else
            {
                // FlatMixed: look up the context property for this level
                if (!levelAccessors.TryGetValue(field.Level, out ownerAccess))
                    continue;
            }

            if (FieldResolver.TryResolveMappedExpression(ownerAccess, field.PropName, options, out var resolvedExpr, out var resolvedType))
            {
                props[field.OutputName] = (resolvedType, resolvedExpr);
                continue;
            }

            var pi = ReflectionCache.GetProperty(ownerAccess.Type, field.PropName);
            if (pi == null) continue;

            props[field.OutputName] = (pi.PropertyType, Expression.Property(ownerAccess, pi));
        }

        if (props.Count == 0)
            return source.Cast<object>();

        var dynType = DynamicTypeBuilder.GetDynamicType(props.ToDictionary(p => p.Key, p => p.Value.Type));
        var newExpr = Expression.New(dynType);
        var bindings = props.Select(p => Expression.Bind(dynType.GetProperty(p.Key)!, p.Value.Expr));
        var memberInit = Expression.MemberInit(newExpr, bindings);
        var lambda = Expression.Lambda(memberInit, param);

        var selectMethod = SelectMethod.MakeGenericMethod(sourceType, dynType);
        var selected = (IQueryable)selectMethod.Invoke(null, [source, lambda])!;
        return selected.Cast<object>();
    }

    // ── Tree decomposition ───────────────────────────────────────────────

    /// <summary>
    /// Recursively decomposes the SelectionNode tree into:
    /// - hops: ordered SelectMany navigation steps
    /// - fields: fields with their level, property name, output name, and CLR type
    ///
    /// parentLevel: -1 = root, 0 = after first hop, etc.
    /// </summary>
    private static (List<NavHop> hops, List<FieldSpec> fields) Decompose(
        SelectionNode tree,
        Type currentType,
        int parentLevel,
        bool allowRootScalars,
        QueryOptions options)
    {
        var hops = new List<NavHop>();
        var fields = new List<FieldSpec>();

        // Pass 1: classify
        var navChildren = new List<(string key, SelectionNode node, PropertyInfo pi, Type elemType)>();
        var leafChildren = new List<(string key, SelectionNode node, PropertyInfo pi)>();

        foreach (var child in tree.EnumerateChildren())
        {
            var propName = child.Key;
            var node = child.Value;

            if (options.Items.TryGetValue(ContextKeys.ExpressionMappings, out var obj) && obj is IReadOnlyDictionary<string, LambdaExpression> mappings)
            {
                if (mappings.TryGetValue(propName, out var mappedLambda))
                {
                    leafChildren.Add((propName, node, null!)); 
                    continue;
                }
            }

            var pi = ReflectionCache.GetProperty(currentType, propName);
            if (pi == null) continue;

            if (pi != null)
            {
                bool isNav = TypeClassification.IsCollectionType(pi.PropertyType, out var elemType)
                             || (!TypeClassification.IsScalarType(pi.PropertyType) && node.HasChildren);

                if (isNav)
                    navChildren.Add((propName, node, pi, elemType ?? pi.PropertyType));
                else if (TypeClassification.IsScalarType(pi.PropertyType))
                    leafChildren.Add((propName, node, pi));
            }
        }

        // Pass 2: validate
        if (navChildren.Count > 1)
            throw new InvalidOperationException(
                "Flat mode does not support branching multiple navigation paths. " +
                "Select a single linear path.");

        if (!allowRootScalars && navChildren.Count > 0 && leafChildren.Count > 0)
            throw new InvalidOperationException(
                "Flat mode does not support mixing scalar properties with nested navigation collections. " +
                "Use mode=flat-mixed or select only from the deepest collection.");

        // Pass 3: build
        // Collect scalars at this level
        foreach (var (propName, node, pi) in leafChildren)
        {
            var outputName = !string.IsNullOrWhiteSpace(node.Alias) ? node.Alias : (pi?.Name ?? propName);
            var propType = pi?.PropertyType;

            if (propType == null && options.Items.TryGetValue(ContextKeys.ExpressionMappings, out var obj) && obj is IReadOnlyDictionary<string, LambdaExpression> mappings && mappings.TryGetValue(propName, out var mappedLambda))
            {
                propType = mappedLambda.ReturnType;
            }

            if (propType != null)
            {
                fields.Add(new FieldSpec(parentLevel, propName, outputName, propType));
            }
        }

        // Recurse into nav children
        foreach (var (propName, node, pi, elemType) in navChildren)
        {
            var nextLevel = parentLevel + 1;
            hops.Add(new NavHop(propName, currentType, elemType));

            if (node.HasChildren)
            {
                // In flat-mixed, we allow scalars at every nav level (intermediate + leaf)
                var (subHops, subFields) = Decompose(node, elemType, nextLevel, allowRootScalars, options);
                hops.AddRange(subHops);
                fields.AddRange(subFields);
            }
            else
            {
                // No children specified → project all scalars of the element type
                foreach (var p in ReflectionCache.GetProperties(elemType))
                {
                    if (TypeClassification.IsScalarType(p.PropertyType))
                        fields.Add(new FieldSpec(nextLevel, p.Name, p.Name, p.PropertyType));
                }
            }
        }

        return (hops, fields);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Builds { "L0": type0, "L1": type1, ... } for a dynamic context type.</summary>
    private static Dictionary<string, Type> BuildLevelTypeFields(List<Type> levelTypes)
    {
        var d = new Dictionary<string, Type>();
        for (int i = 0; i < levelTypes.Count; i++)
            d[$"L{i}"] = levelTypes[i];
        return d;
    }
}
