using System.Linq.Expressions;
using System.Reflection;
using FlexQuery.NET.Caching;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Expressions;
using FlexQuery.NET.Resolvers;
using FlexQuery.NET.Mapping;
using FlexQuery.NET.Metadata;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.QuerySurface;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Projection;

/// <summary>
/// Builds the server-side DTO projection expression for typed DTO queries.
/// Navigation members are projected through the mapping registry's TypeMap graph:
/// nested DTO element types are materialized recursively via their registered
/// <c>TypeMap</c> — raw entity types never leak into nested DTO graphs.
/// </summary>
internal static class DtoProjectionBuilder
{
    private const int MaxNestedProjectionDepth = 8;

    private static readonly MethodInfo EnumerableSelectMethod = typeof(Enumerable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .First(m => m is { Name: nameof(Enumerable.Select), IsGenericMethodDefinition: true }
                    && m.GetParameters().Length == 2);

    private static readonly MethodInfo EnumerableToListMethod = typeof(Enumerable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .First(m => m is { Name: nameof(Enumerable.ToList), IsGenericMethodDefinition: true }
                    && m.GetParameters().Length == 1);

    public static Expression<Func<TEntity, TResponse>> Build<TEntity, TResponse>(
        QueryOptions queryOptions,
        IQuerySurface surface,
        IReadOnlyList<SelectNode>? fieldsOverride = null,
        IReadOnlyDictionary<string, LambdaExpression>? navigationWindows = null,
        IQueryMappingRegistry? mappingRegistry = null,
        IReadOnlyDictionary<string, ExpandWindowNode>? expandWindows = null)
        where TEntity : class
        where TResponse : class
    {
        var entityParam = Expression.Parameter(typeof(TEntity), "x");
        var responseType = typeof(TResponse);

        var allowedNavigationPaths = RequestedNavigationGraph.Collect(queryOptions);

        var fieldsToProject = fieldsOverride ??
            (queryOptions.Select is { Count: > 0 }
                ? queryOptions.Select
                : surface.GetDefaultSelectFields().Select(f => new SelectNode { Field = f }).ToList());

        var bindings = new List<MemberBinding>();
        foreach (var selectNode in fieldsToProject)
        {
            var sourceField = selectNode.Field;

            if (!surface.TryResolve(sourceField ?? "", out var resolved))
            {
                throw new FlexQueryException(
                    $"Cannot project source field '{sourceField}' on {responseType.Name}. " +
                    $"The field has no entity mapping. Call MapField<{responseType.Name}>(x => x.{sourceField}, e => e.<property>) " +
                    $"or ensure the property name matches an entity property.");
            }

            // Computed scalar mappings (MapField(alias, computedExpression)) bind to the
            // same-named DTO property; the surface carries the entity expression only.
            var responseProp = resolved.ResponseProperty
                ?? ReflectionCache.GetProperty(responseType, resolved.SurfaceName);

            if (responseProp is null)
            {
                throw new FlexQueryException(
                    $"Cannot project source field '{sourceField}' on {responseType.Name}. " +
                    $"The field has no entity mapping. Call MapField<{responseType.Name}>(x => x.{sourceField}, e => e.<property>) " +
                    $"or ensure the property name matches an entity property.");
            }

            if (!responseProp.CanWrite)
            {
                throw new FlexQueryException(
                    $"Typed response '{responseType.Name}' property '{responseProp.Name}' for source field '{sourceField}' is not writable.");
            }

            var entityExprBody = ReplaceParameter(resolved.EntityExpression, entityParam);

            if (resolved.IsNavigation)
            {
                // Deep expansion window for this navigation (flat dotted path normalized
                // into a hierarchical tree); child windows are applied per level below.
                ExpandWindowNode? expandWindow = null;
                var hasExpandWindow = expandWindows != null
                    && expandWindows.TryGetValue(resolved.EntityProperty.Name, out expandWindow);

                LambdaExpression? window = null;
                var hasWindow = navigationWindows != null
                    && navigationWindows.TryGetValue(resolved.EntityProperty.Name, out window);

                if (hasExpandWindow)
                {
                    // Deep expansion: apply this level's filter/sort/take directly to the
                    // navigation body, then recurse into child windows — the child window
                    // applies to the element collections of the already-windowed parent,
                    // keeping nested processing correlated to the selected parent rows.
                    var windowBody = ApplyExpandWindow(entityExprBody, expandWindow!, queryOptions);

                    if (selectNode.Children is { Count: > 0 })
                    {
                        entityExprBody = BuildNestedChildProjection(
                            windowBody, selectNode.Children, responseProp.PropertyType, queryOptions, mappingRegistry,
                            expandChildren: expandWindow!.Children);
                    }
                    else
                    {
                        entityExprBody = ProjectNestedMemberNavigation(
                            windowBody, responseProp.PropertyType, mappingRegistry!, depth: 1,
                            expandWindow!.Children, resolved.EntityProperty.Name, allowedNavigationPaths);
                    }
                }
                else if (selectNode.Children is { Count: > 0 })
                {
                    // Nested root-select tree (e.g. select=...,Orders(OrderId,Status)):
                    // expand = which records; select = which fields. Combine both into one
                    // server-side expression tree: window → Select(child projections) → ToList.
                    var windowBody = hasWindow ? ReplaceParameter(window!, entityParam) : entityExprBody;
                    entityExprBody = BuildNestedChildProjection(
                        windowBody, selectNode.Children, responseProp.PropertyType, queryOptions, mappingRegistry,
                        expandChildren: null);
                }
                else if (hasWindow)
                {
                    // Server-side expand window bound directly; DTO-typed navigation element
                    // types project through the registered nested TypeMap graph.
                    entityExprBody = ProjectNestedMemberNavigation(
                        ReplaceParameter(window!, entityParam), responseProp.PropertyType, mappingRegistry!, depth: 1, null,
                        resolved.EntityProperty.Name, allowedNavigationPaths);
                }
                else
                {
                    // Include/Expand materialization binds entity collections into the response.
                    // Cut the graph at the entity boundary for entity-typed members; DTO-typed
                    // members project through the registered nested TypeMap graph.
                    entityExprBody = mappingRegistry is not null
                        ? ProjectNestedMemberNavigation(entityExprBody, responseProp.PropertyType, mappingRegistry, depth: 1,
                            expandChildren: null, navigationPath: resolved.EntityProperty.Name, allowedNavigationPaths: allowedNavigationPaths)
                        : BuildNavigationCutProjection(entityExprBody, responseProp.PropertyType);
                }
            }

            if (entityExprBody.Type != responseProp.PropertyType)
            {
                if (!responseProp.PropertyType.IsAssignableFrom(entityExprBody.Type)
                    && !entityExprBody.Type.IsAssignableFrom(responseProp.PropertyType))
                {
                    throw new FlexQueryException(
                        $"Property type mismatch for '{sourceField}': DTO property is {responseProp.PropertyType.Name}, " +
                        $"entity property is {resolved.EntityProperty.PropertyType.Name}. Types are incompatible.");
                }

                entityExprBody = Expression.Convert(entityExprBody, responseProp.PropertyType);
            }

            var binding = Expression.Bind(responseProp, entityExprBody);
            bindings.Add(binding);
        }

        var body = Expression.MemberInit(Expression.New(responseType), bindings);
        return Expression.Lambda<Func<TEntity, TResponse>>(body, entityParam);
    }

    /// <summary>
    /// Applies one expansion level's filter/sort/take directly to a navigation body:
    /// <c>nav.Where(...).OrderBy(...).Take(n)</c>. EF Core translates this into a
    /// correlated APPLY-shaped window restricted to the parent element — never an
    /// uncorrelated whole-child-table ranking.
    /// </summary>
    private static Expression ApplyExpandWindow(
        Expression navigationBody,
        ExpandWindowNode window,
        QueryOptions queryOptions)
    {
        var node = window.Node;
        var elementType = UnwrapCollection(navigationBody.Type);

        var body = navigationBody;

        if (node.Filter is not null)
        {
            var predicate = ExpressionBuilder.BuildPredicate(elementType, new QueryOptions
            {
                Filter = node.Filter,
                EnableCache = queryOptions.EnableCache,
                UseEfCoreOperators = queryOptions.UseEfCoreOperators
            });

            if (predicate is not null)
            {
                var whereMethod = typeof(Enumerable)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(m => m is { Name: nameof(Enumerable.Where), IsGenericMethodDefinition: true }
                                && m.GetParameters().Length == 2)
                    .MakeGenericMethod(elementType);
                body = Expression.Call(whereMethod, body, predicate);
            }
        }

        if (node.Sort is { Count: > 0 })
        {
            var result = body;
            var ordered = false;

            foreach (var sortNode in node.Sort)
            {
                var parameter = Expression.Parameter(elementType, "o");

                // Expand sort fields are entity-level names on the element type. Resolve
                // to a plain member access (translatable in the correlated projection —
                // SortBuilder's mapped/binding forms can require APPLY on some providers).
                var keyProp = ReflectionCache.GetProperty(elementType, sortNode.Field);
                if (keyProp is null)
                    continue;

                var keyExpression = Expression.Property(parameter, keyProp);
                var selectorType = typeof(Func<,>).MakeGenericType(elementType, keyExpression.Type);
                var selector = Expression.Lambda(selectorType, keyExpression, parameter);
                var methodName = (ordered, sortNode.Descending) switch
                {
                    (false, false) => nameof(Enumerable.OrderBy),
                    (false, true) => nameof(Enumerable.OrderByDescending),
                    (true, false) => nameof(Enumerable.ThenBy),
                    (true, true) => nameof(Enumerable.ThenByDescending)
                };

                var orderMethod = typeof(Enumerable)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(m => m.Name == methodName && m.IsGenericMethodDefinition
                                && m.GetGenericArguments().Length == 2
                                && m.GetParameters().Length == 2)
                    .MakeGenericMethod(elementType, keyExpression.Type);
                result = Expression.Call(orderMethod, result, selector);
                ordered = true;
            }

            body = result;
        }

        if (node.Take is > 0)
        {
            var takeMethod = typeof(Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m is { Name: nameof(Enumerable.Take), IsGenericMethodDefinition: true }
                            && m.GetParameters().Length == 2)
                .MakeGenericMethod(elementType);
            body = Expression.Call(takeMethod, body, Expression.Constant(node.Take.Value));
        }

        return body;
    }

    private static Type UnwrapCollection(Type type)
        => SafePropertyResolver.TryGetCollectionElementType(type, out var element)
            ? element
            : type;

    /// <summary>
    /// Projects a nested navigation member inside a TypeMap-driven element projection.
    /// When the member's DTO type differs from the entity type, the registered nested
    /// <c>TypeMap</c> drives a recursive server-side projection — raw entity types never
    /// leak into nested DTO graphs. Entity-typed members keep the established cut
    /// semantics; unmapped DTO-typed members bind null (never raw entity graphs).
    /// </summary>
    private static Expression ProjectNestedMemberNavigation(
        Expression navigationBody,
        Type responseType,
        IQueryMappingRegistry registry,
        int depth,
        IReadOnlyDictionary<string, ExpandWindowNode>? expandChildren,
        string navigationPath,
        IReadOnlySet<string> allowedNavigationPaths)
    {
        if (navigationBody.Type == typeof(string))
            return navigationBody;

        var isCollection = SafePropertyResolver.TryGetCollectionElementType(responseType, out var dtoElement);
        var entityElementType = ResolveEntityElementType(navigationBody, isCollection ? dtoElement : null);

        if (isCollection
            && dtoElement != entityElementType
            && registry.Find(entityElementType, dtoElement) is { } nestedCollectionMap)
        {
            var elementParam = Expression.Parameter(entityElementType, "e");
            var memberInit = BuildTypeMapMemberInit(nestedCollectionMap, registry, depth, elementParam, expandChildren,
                navigationPath, allowedNavigationPaths);
            var selector = Expression.Lambda(memberInit, elementParam);

            var selectCall = Expression.Call(
                EnumerableSelectMethod.MakeGenericMethod(entityElementType, dtoElement),
                navigationBody,
                selector);
            
            var toList = Expression.Call(EnumerableToListMethod.MakeGenericMethod(dtoElement), selectCall);
            return responseType.IsAssignableFrom(toList.Type)
                ? toList.Type == responseType ? toList : Expression.Convert(toList, responseType)
                : toList;
        }

        if (!isCollection
            && responseType != navigationBody.Type
            && registry.Find(navigationBody.Type, responseType) is { } nestedReferenceMap)
        {
            var referenceParam = Expression.Parameter(navigationBody.Type, "e");
            var memberInit = BuildTypeMapMemberInit(nestedReferenceMap, registry, depth, referenceParam, expandChildren,
                navigationPath, allowedNavigationPaths);
            var projectedBody = ReplaceParameter(
                Expression.Lambda(memberInit, referenceParam),
                navigationBody);
            var notNull = Expression.NotEqual(navigationBody, Expression.Constant(null, navigationBody.Type));
            var nullValue = Expression.Constant(null, responseType);

            return responseType.IsAssignableFrom(projectedBody.Type)
                ? Expression.Condition(notNull, Expression.Convert(projectedBody, responseType), nullValue)
                : projectedBody;
        }

        // DTO-typed member (destination element type differs from the entity element
        // type) with no registered nested map: bind null — a raw entity graph must never
        // leak into a DTO-typed navigation property. Entity-typed members keep the cut
        // semantics (the DTO author opted into the entity shape).
        if (isCollection
            && dtoElement is not null
            && dtoElement != entityElementType)
        {
            return Expression.Constant(null, responseType);
        }

        if (!isCollection && responseType != navigationBody.Type)
        {
            return Expression.Constant(null, responseType);
        }

        return BuildNavigationCutProjection(navigationBody, responseType);
    }

    /// <summary>
    /// Builds a nested child projection over a navigation window:
    /// <c>window.Select(e => new TElement { child fields }).ToList()</c> for collections, or
    /// <c>window == null ? null : new TElement { ... }</c> for reference navigations.
    /// Child public field names resolve through the registered nested <c>TypeMap</c> when
    /// one exists (DTO member names), falling back to the element's dual surface.
    /// </summary>
    private static Expression BuildNestedChildProjection(
        Expression windowBody,
        IReadOnlyList<SelectNode> childNodes,
        Type responseType,
        QueryOptions queryOptions,
        IQueryMappingRegistry? mappingRegistry,
        IReadOnlyDictionary<string, ExpandWindowNode>? expandChildren = null)
    {
        Type elementType;
        Type projectionType;
        var isCollection = SafePropertyResolver.TryGetCollectionElementType(responseType, out var collectionElement);

        if (isCollection)
        {
            elementType = SafePropertyResolver.TryGetCollectionElementType(windowBody.Type, out var windowElement)
                ? windowElement
                : collectionElement!;
            projectionType = collectionElement!;
        }
        else
        {
            elementType = windowBody.Type;
            projectionType = responseType;
        }

        var nestedMap = mappingRegistry?.Find(elementType, projectionType);
        var elementProperties = ReflectionCache.GetProperties(projectionType).ToList();
        var entityPropsByName = ReflectionCache.GetProperties(elementType)
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        var elementParam = Expression.Parameter(elementType, "e");
        var childBindings = new List<MemberBinding>();

        foreach (var child in childNodes)
        {
            var childName = child.Field;

            PropertyInfo? responseProp;
            Expression childAccess;

            if (nestedMap is not null && nestedMap.TryResolveDestinationMember(childName, out var memberMap))
            {
                // Nested TypeMap member: DTO member name + entity expression from the map.
                responseProp = memberMap.DestinationProperty
                    ?? elementProperties.FirstOrDefault(
                        p => p.Name.Equals(memberMap.DestinationName, StringComparison.OrdinalIgnoreCase) && p.CanWrite);

                if (responseProp is null)
                {
                    throw new FlexQueryException(
                        $"Cannot project nested field '{childName}' on '{projectionType.Name}'. " +
                        $"Ensure the property exists on the nested response type and is writable.");
                }

                childAccess = ReplaceParameter(memberMap.SourceExpression, elementParam);
            }
            else
            {
                responseProp = elementProperties.FirstOrDefault(
                    p => p.Name.Equals(childName, StringComparison.OrdinalIgnoreCase) && p.CanWrite);

                if (responseProp is null)
                {
                    throw new FlexQueryException(
                        $"Cannot project nested field '{childName}' on '{projectionType.Name}'. " +
                        $"Ensure the property exists on the nested response type and is writable.");
                }

                if (!entityPropsByName.TryGetValue(responseProp.Name, out var entityProp))
                {
                    throw new FlexQueryException(
                        $"Cannot project nested field '{childName}' on '{projectionType.Name}'. " +
                        $"The field has no entity mapping on '{elementType.Name}'.");
                }

                childAccess = Expression.Property(elementParam, entityProp);
            }

            if (child.Children is { Count: > 0 })
            {
                childAccess = BuildNestedChildProjection(
                    childAccess, child.Children, responseProp.PropertyType, queryOptions, mappingRegistry);
            }
            else if (!TypeClassification.IsScalarType(responseProp.PropertyType))
            {
                // Deep expansion windows apply to this child navigation when the expand
                // tree declares it; otherwise the entity graph cut applies.
                ExpandWindowNode? childWindow = null;
                expandChildren?.TryGetValue(responseProp.Name, out childWindow);

                if (childWindow is not null)
                {
                    childAccess = ApplyExpandWindow(childAccess, childWindow, queryOptions);
                }
                else
                {
                    // Deep navigation children inside a nested select keep the entity graph cut:
                    // scalar-only shape so no reverse navigation or collection graph leaks.
                    childAccess = BuildNavigationCutProjection(childAccess, responseProp.PropertyType);
                }
            }

            childBindings.Add(Expression.Bind(responseProp, childAccess));
        }

        var memberInit = Expression.MemberInit(Expression.New(projectionType), childBindings);
        var selector = Expression.Lambda(memberInit, elementParam);

        if (isCollection)
        {
            var selectCall = Expression.Call(
                EnumerableSelectMethod.MakeGenericMethod(elementType, projectionType),
                windowBody,
                selector);
            var toList = Expression.Call(EnumerableToListMethod.MakeGenericMethod(projectionType), selectCall);
            return responseType.IsAssignableFrom(toList.Type)
                ? (toList.Type == responseType ? toList : Expression.Convert(toList, responseType))
                : toList;
        }

        // Reference navigation.
        var projectedBody = ReplaceParameter(selector, windowBody);
        var notNull = Expression.NotEqual(windowBody, Expression.Constant(null, windowBody.Type));
        var nullValue = Expression.Constant(null, responseType);

        return responseType.IsAssignableFrom(projectedBody.Type)
            ? Expression.Condition(notNull, Expression.Convert(projectedBody, responseType), nullValue)
            : projectedBody;
    }

    /// <summary>
    /// Builds <c>e => new TDestination { ... }</c> from a <see cref="ITypeMap"/>,
    /// recursively projecting navigation members through nested registered type maps.
    /// Child expansion windows apply to navigation members declared in the nested graph.
    /// </summary>
    private static Expression BuildTypeMapMemberInit(
        ITypeMap map,
        IQueryMappingRegistry registry,
        int depth,
        ParameterExpression elementParam,
        IReadOnlyDictionary<string, ExpandWindowNode>? expandChildren = null,
        string? navigationPath = null,
        IReadOnlySet<string>? allowedNavigationPaths = null)
    {
        var bindings = new List<MemberBinding>();

        foreach (var propertyMap in map.PropertyMaps)
        {
            var destProp = propertyMap.DestinationProperty;
            if (destProp is null || !destProp.CanWrite)
                continue;

            Expression value = ReplaceParameter(propertyMap.SourceExpression, elementParam);

            if (propertyMap.IsNavigation && depth < MaxNestedProjectionDepth)
            {
                var entityNavName = propertyMap.SourceProperty?.Name ?? destProp.Name;
                var childPath = navigationPath is null ? null : $"{navigationPath}.{entityNavName}";
                var destinationPath = navigationPath is null ? null : $"{navigationPath}.{destProp.Name}";

                if (navigationPath is not null
                    && allowedNavigationPaths is not null
                    && (childPath is null
                        || !(allowedNavigationPaths.Contains(childPath)
                             || (destinationPath is not null && allowedNavigationPaths.Contains(destinationPath)))))
                {
                    continue;
                }

                ExpandWindowNode? childWindow = null;
                if (expandChildren is not null)
                {
                    if (!expandChildren.TryGetValue(destProp.Name, out childWindow))
                        expandChildren.TryGetValue(entityNavName, out childWindow);
                }

                if (childWindow is not null)
                {
                    // Apply the child window's own options (filter/sort/take) to the
                    // navigation body, correlated to the current element.
                    value = ApplyExpandWindow(value, childWindow, new QueryOptions());
                }

                if (childPath != null && allowedNavigationPaths != null)
                        value = ProjectNestedMemberNavigation(value, destProp.PropertyType, registry, depth + 1,
                            childWindow?.Children, childPath, allowedNavigationPaths);
            }

            if (value.Type != destProp.PropertyType)
            {
                if (!destProp.PropertyType.IsAssignableFrom(value.Type)
                    && !value.Type.IsAssignableFrom(destProp.PropertyType))
                {
                    continue;
                }

                value = Expression.Convert(value, destProp.PropertyType);
            }

            bindings.Add(Expression.Bind(destProp, value));
        }

        return Expression.MemberInit(Expression.New(map.DestinationType), bindings);
    }

    private static Type ResolveEntityElementType(Expression navigationBody, Type? dtoElement)
    {
        return SafePropertyResolver.TryGetCollectionElementType(navigationBody.Type, out var entityElement) 
            ? entityElement : navigationBody.Type;
    }

    /// <summary>
    /// Rewrites a navigation access so the projected value contains only scalar properties.
    /// Collections become <c>nav.Select(e => new T { scalars }).ToList()</c>; reference
    /// navigations become <c>nav == null ? null : new T { scalars }</c>. Returns the original
    /// expression unchanged when the target shape cannot be projected (no parameterless
    /// constructor, or the type has no navigations to cut).
    /// </summary>
    private static Expression BuildNavigationCutProjection(Expression navigationBody, Type responseType)
    {
        if (navigationBody.Type == typeof(string))
            return navigationBody;

        if (SafePropertyResolver.TryGetCollectionElementType(responseType, out var responseElementType))
        {
            if (!SafePropertyResolver.TryGetCollectionElementType(navigationBody.Type, out var navElementType))
            {
                return navigationBody;
            }

            var cutSelector = TryBuildScalarCutLambda(navElementType, responseElementType);
            if (cutSelector is null)
                return navigationBody;

            var selectCall = Expression.Call(
                EnumerableSelectMethod.MakeGenericMethod(navElementType, responseElementType),
                navigationBody,
                cutSelector);

            Expression result = responseType.IsAssignableFrom(selectCall.Type)
                ? selectCall
                : Expression.Call(EnumerableToListMethod.MakeGenericMethod(responseElementType), selectCall);

            return responseType.IsAssignableFrom(result.Type)
                ? (result.Type == responseType ? result : Expression.Convert(result, responseType))
                : navigationBody;
        }

        // Reference navigation: project scalars when non-null, otherwise null.
        var cutBody = TryBuildScalarCutLambda(navigationBody.Type, responseType);
        if (cutBody is null)
            return navigationBody;

        var projectedBody = ReplaceParameter(cutBody, navigationBody);
        var notNull = Expression.NotEqual(navigationBody, Expression.Constant(null, navigationBody.Type));
        var nullValue = Expression.Constant(null, responseType);

        return responseType.IsAssignableFrom(projectedBody.Type)
            ? Expression.Condition(notNull, Expression.Convert(projectedBody, responseType), nullValue)
            : navigationBody;
    }

    /// <summary>
    /// Builds <c>e => new TTarget { scalar props }</c> when TTarget is TSource and TSource
    /// has at least one navigation property to cut; otherwise null (passthrough).
    /// </summary>
    private static LambdaExpression? TryBuildScalarCutLambda(Type sourceType, Type targetType)
    {
        if (sourceType != targetType
            || targetType.IsValueType
            || targetType.GetConstructor(Type.EmptyTypes) is null)
        {
            return null;
        }

        var properties = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var hasNavigation = properties.Any(p =>
            p.CanRead && !TypeClassification.IsScalarType(p.PropertyType));
        if (!hasNavigation)
            return null;

        var scalarProps = properties
            .Where(p => p is { CanRead: true, CanWrite: true } && TypeClassification.IsScalarType(p.PropertyType))
            .ToList();

        var parameter = Expression.Parameter(sourceType, "e");
        var cutBindings = scalarProps
            .Select(p => Expression.Bind(p, Expression.Property(parameter, p)));
        var body = Expression.MemberInit(Expression.New(targetType), cutBindings);

        return Expression.Lambda(body, parameter);
    }

    private static Expression ReplaceParameter(LambdaExpression lambda, Expression replacement)
    {
        var map = new Dictionary<ParameterExpression, Expression>
        {
            { lambda.Parameters[0], replacement }
        };
        return ParameterRebinder.ReplaceParameters(map, lambda.Body);
    }
}

