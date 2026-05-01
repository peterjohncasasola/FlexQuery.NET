using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Models;
using FlexQuery.NET.Security;
using FlexQuery.NET.Helpers;

namespace FlexQuery.NET.Builders;

/// <summary>
/// Builds dynamic flat / flat-mixed projections using SelectMany expression chains.
/// <list type="bullet">
///   <item><term>Flat</term>       – single linear nav path, leaf-only rows.</item>
///   <item><term>FlatMixed</term>  – correlated SelectMany, all levels (root + intermediate + leaf) in one flat row.</item>
/// </list>
///
/// For FlatMixed with N navigation hops, the expression tree becomes:
/// <code>
///   query
///     .SelectMany(c => c.Orders,    (c, o)  => new { L0 = c, L1 = o })
///     .SelectMany(x => x.L1.Items,  (x, oi) => new { customerId = x.L0.Id, orderStatus = x.L1.Status, product = oi.ProductName })
/// </code>
/// </summary>
internal static class FlatProjectionBuilder
{
    // ── Reflected Queryable methods ──────────────────────────────────────

    private static readonly MethodInfo SelectManyMethod = typeof(Queryable).GetMethods()
        .First(m => m.Name == nameof(Queryable.SelectMany)
                    && m.GetParameters().Length == 2
                    && m.GetParameters()[1].ParameterType.IsGenericType
                    && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>)
                    && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2);

    // 3-param overload: SelectMany(source, collectionSelector, resultSelector)
    private static readonly MethodInfo SelectManyWithResultMethod = typeof(Queryable).GetMethods()
        .First(m => m.Name == nameof(Queryable.SelectMany)
                    && m.GetParameters().Length == 3
                    && m.GetParameters()[1].ParameterType.IsGenericType
                    && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>)
                    && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2);

    private static readonly MethodInfo SelectMethod = typeof(Queryable).GetMethods()
        .First(m => m.Name == nameof(Queryable.Select)
                    && m.GetParameters().Length == 2
                    && m.GetParameters()[1].ParameterType.IsGenericType
                    && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>)
                    && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2);

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
    public static IQueryable<object> BuildAndApply<T>(IQueryable<T> query, SelectionNode tree)
    {
        var (hops, fields) = Decompose(tree, typeof(T), parentLevel: -1, allowRootScalars: false);
        return ApplyFlatChain(query, typeof(T), hops, fields);
    }

    /// <summary>FlatMixed mode: root + intermediate + leaf fields all in one flat row.</summary>
    public static IQueryable<object> BuildAndApplyMixed<T>(IQueryable<T> query, SelectionNode tree)
    {
        var (hops, fields) = Decompose(tree, typeof(T), parentLevel: -1, allowRootScalars: true);
        return ApplyFlatMixedChain(query, typeof(T), hops, fields);
    }

    // ── Flat (strict, leaf-only) ─────────────────────────────────────────

    private static IQueryable<object> ApplyFlatChain(
        IQueryable source,
        Type currentType,
        List<NavHop> hops,
        List<FieldSpec> fields)
    {
        // Walk the navigation path using single-arg SelectMany
        foreach (var hop in hops)
        {
            var prop = hop.SourceType.GetProperty(hop.PropName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!;
            var param = Expression.Parameter(hop.SourceType, "x");
            var propAccess = Expression.Property(param, prop);

            var enumType = typeof(IEnumerable<>).MakeGenericType(hop.ElementType);
            var funcType = typeof(Func<,>).MakeGenericType(hop.SourceType, enumType);
            var lambda = Expression.Lambda(funcType, propAccess, param);

            var method = SelectManyMethod.MakeGenericMethod(hop.SourceType, hop.ElementType);
            source = (IQueryable)method.Invoke(null, [source, lambda])!;
            currentType = hop.ElementType;
        }

        // Project only leaf fields
        return ProjectToFlatRow(source, currentType, fields, levelAccessors: null);
    }

    // ── FlatMixed ────────────────────────────────────────────────────────

    private static IQueryable<object> ApplyFlatMixedChain(
        IQueryable source,
        Type rootType,
        List<NavHop> hops,
        List<FieldSpec> fields)
    {
        if (hops.Count == 0)
        {
            // No navigation — just project root scalars
            return ProjectToFlatRow(source, rootType, fields, levelAccessors: null);
        }

        // We use the 3-param SelectMany to carry a context object through the chain.
        // After N hops, the current element type is a flat context carrying
        // all entities at all levels: { L0: Customer, L1: Order, L2: OrderItem, ... }

        // Build level-name mapping: level -1 → root, level 0 → L0, level 1 → L1, ...
        // We'll build a context type that holds all levels seen so far.
        // At each step: ctx = new { L0 = prevCtx.L0, L1 = prevCtx.L1, ..., Ln = item }

        // Step 1: build contextual type progressively
        // After the first hop:  contextType = { L0: rootType, L1: hop0.ElementType }
        // After the second hop: contextType = { L0: rootType, L1: hop0.Element, L2: hop1.Element }

        IQueryable currentQuery = source;
        Type currentType = rootType;

        // Track: (parameterExpression, type) at each level for building context accesses
        // We simulate a growing flat context type.
        var levelTypes = new List<Type> { rootType }; // levelTypes[0] = rootType
        Type? contextType = null;

        for (int i = 0; i < hops.Count; i++)
        {
            var hop = hops[i];
            var prop = currentType.GetProperty(hop.PropName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!;

            if (i == 0)
            {
                // First hop: collectionSelector = root => root.NavProp
                // resultSelector = (root, item) => new { L0 = root, L1 = item }
                var rootParam = Expression.Parameter(rootType, "root");
                var itemParam = Expression.Parameter(hop.ElementType, "item0");

                var propAccess = Expression.Property(rootParam, prop);
                var enumType = typeof(IEnumerable<>).MakeGenericType(hop.ElementType);
                var collFuncType = typeof(Func<,>).MakeGenericType(rootType, enumType);
                var collSelector = Expression.Lambda(collFuncType, propAccess, rootParam);

                // Build context type: { L0: rootType, L1: hop.ElementType }
                levelTypes.Add(hop.ElementType); // levelTypes[1] = hop.ElementType
                var ctxFields = BuildLevelTypeFields(levelTypes);
                contextType = DynamicTypeBuilder.GetDynamicType(ctxFields);

                var ctxNew = Expression.New(contextType);
                var ctxBindings = new List<MemberBinding>
                {
                    Expression.Bind(contextType.GetProperty("L0")!, rootParam),
                    Expression.Bind(contextType.GetProperty("L1")!, itemParam)
                };
                var ctxInit = Expression.MemberInit(ctxNew, ctxBindings);
                var resFuncType = typeof(Func<,,>).MakeGenericType(rootType, hop.ElementType, contextType);
                var resSelector = Expression.Lambda(resFuncType, ctxInit, rootParam, itemParam);

                var method = SelectManyWithResultMethod.MakeGenericMethod(rootType, hop.ElementType, contextType);
                currentQuery = (IQueryable)method.Invoke(null, [currentQuery, collSelector, resSelector])!;
                currentType = contextType;
            }
            else
            {
                // Subsequent hops: contextType is the accumulator
                // collectionSelector = ctx => ctx.L{i}.NavProp
                // resultSelector = (ctx, item) => new { L0 = ctx.L0, L1 = ctx.L1, ..., L{i+1} = item }
                var ctxParam = Expression.Parameter(currentType, $"ctx{i}");
                var itemParam = Expression.Parameter(hop.ElementType, $"item{i}");

                // Navigate: ctx.Li (previous item)
                var prevLevelProp = currentType.GetProperty($"L{i}")!;
                var prevLevelAccess = Expression.Property(ctxParam, prevLevelProp);
                var navProp = hop.SourceType.GetProperty(hop.PropName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!;
                var propAccess = Expression.Property(prevLevelAccess, navProp);

                var enumType = typeof(IEnumerable<>).MakeGenericType(hop.ElementType);
                var collFuncType = typeof(Func<,>).MakeGenericType(currentType, enumType);
                var collSelector = Expression.Lambda(collFuncType, propAccess, ctxParam);

                // Build new expanded context type: add L{i+1}
                levelTypes.Add(hop.ElementType);
                var ctxFields = BuildLevelTypeFields(levelTypes);
                var newContextType = DynamicTypeBuilder.GetDynamicType(ctxFields);

                // Build result: new { L0 = ctx.L0, L1 = ctx.L1, ..., Li = ctx.Li, L{i+1} = item }
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

                var method = SelectManyWithResultMethod.MakeGenericMethod(currentType, hop.ElementType, newContextType);
                currentQuery = (IQueryable)method.Invoke(null, [currentQuery, collSelector, resSelector])!;
                currentType = newContextType;
                contextType = newContextType;
            }
        }

        // Build a lookup from level index to the context property expression
        // Level -1 → L0 (root), Level 0 → L1 (first hop), Level 1 → L2, ...
        var ctxFinalParam = Expression.Parameter(currentType, "ctx");
        var levelAccessors = new Dictionary<int, Expression>();
        for (int lvl = 0; lvl < levelTypes.Count; lvl++)
        {
            var p = currentType.GetProperty($"L{lvl}");
            if (p != null)
                levelAccessors[lvl - 1] = Expression.Property(ctxFinalParam, p); // root=-1 → L0
        }

        return ProjectToFlatRow(currentQuery, currentType, fields, levelAccessors, ctxFinalParam);
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

            var pi = ownerAccess.Type.GetProperty(field.PropName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
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
        bool allowRootScalars)
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
            var pi = currentType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (pi == null) continue;

            bool isNav = IsCollectionType(pi.PropertyType, out var elemType)
                         || (!IsScalarType(pi.PropertyType) && node.HasChildren);

            if (isNav)
                navChildren.Add((propName, node, pi, elemType ?? pi.PropertyType));
            else if (IsScalarType(pi.PropertyType))
                leafChildren.Add((propName, node, pi));
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
            var outputName = !string.IsNullOrWhiteSpace(node.Alias) ? node.Alias : pi.Name;
            fields.Add(new FieldSpec(parentLevel, propName, outputName, pi.PropertyType));
        }

        // Recurse into nav children
        foreach (var (propName, node, pi, elemType) in navChildren)
        {
            var nextLevel = parentLevel + 1;
            hops.Add(new NavHop(propName, currentType, elemType));

            if (node.HasChildren)
            {
                // In flat-mixed, we allow scalars at every nav level (intermediate + leaf)
                var (subHops, subFields) = Decompose(node, elemType, nextLevel, allowRootScalars);
                hops.AddRange(subHops);
                fields.AddRange(subFields);
            }
            else
            {
                // No children specified → project all scalars of the element type
                foreach (var p in elemType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (IsScalarType(p.PropertyType))
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

    private static bool IsCollectionType(Type type, out Type itemType)
        => SafePropertyResolver.TryGetCollectionElementType(type, out itemType);

    private static bool IsScalarType(Type type)
    {
        var unwrapped = Nullable.GetUnderlyingType(type) ?? type;
        return unwrapped.IsPrimitive
            || unwrapped.IsEnum
            || unwrapped == typeof(string)
            || unwrapped == typeof(decimal)
            || unwrapped == typeof(DateTime)
            || unwrapped == typeof(DateTimeOffset)
            || unwrapped == typeof(Guid)
            || unwrapped == typeof(TimeSpan)
            || unwrapped == typeof(DateOnly)
            || unwrapped == typeof(TimeOnly);
    }
}
