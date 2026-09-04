using System.Reflection;
using System.Linq.Expressions;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Mapping;
using FlexQuery.NET.Metadata;
using FlexQuery.NET.Options;
using FlexQuery.NET.Caching;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.QuerySurface;

/// <summary>
/// Builds <see cref="IQuerySurface"/> instances from entity type, optional response type,
/// and per-request options.
/// </summary>
public static class QuerySurfaceBuilder
{
    /// <summary>
    /// Builds an immutable <see cref="IQuerySurface"/>.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <param name="responseType">The optional response/DTO type.</param>
    /// <param name="options">The per-request options, which may contain typed DTO mappings.</param>
    /// <returns>An immutable <see cref="IQuerySurface"/>.</returns>
    /// <exception cref="FlexQueryException">Thrown when typed DTO mappings are registered for the wrong type pair.</exception>
    public static IQuerySurface Build(Type entityType, Type? responseType, BaseQueryOptions options)
    {
        var fieldNameComparer = StringComparer.OrdinalIgnoreCase;
        var entityScalarProperties = ReflectionCache.GetProperties(entityType)
            .Where(p => TypeClassification.IsScalarType(p.PropertyType))
            .ToList();

        IReadOnlyList<PropertyInfo> responseScalarProperties = [];
        Dictionary<string, ResolvedField> resolvableFields = new(fieldNameComparer);

        if (responseType == null)
            return new QuerySurface(
                entityType,
                responseType,
                fieldNameComparer,
                resolvableFields.Values.ToList(),
                responseScalarProperties,
                entityScalarProperties);
        {
            var responseProperties = ReflectionCache.GetProperties(responseType).ToList();
            responseScalarProperties = responseProperties
                .Where(p => TypeClassification.IsScalarType(p.PropertyType))
                .ToList();

            var entityPropsByName = ReflectionCache.GetProperties(entityType)
                .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

            // Primary: the AutoMapper-style mapping registry (CreateMap/ForMember).
            // This is the canonical explicit mapping source; legacy MapField DTO mappings
            // below remain supported and feed the same infrastructure.
            ITypeMap? registryTypeMap = null;
            if (options.MappingRegistry is { } registry)
            {
                registryTypeMap = registry.Find(entityType, responseType);
            }

            // Build explicit mapping lookup from typed DTO mappings
            Dictionary<string, TypedDtoMapping>? explicitMappings = null;
            if (options.DtoFieldMappings is { Count: > 0 })
            {
                explicitMappings = new Dictionary<string, TypedDtoMapping>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in options.DtoFieldMappings)
                {
                    if (kvp.Value.DtoType != responseType)
                        throw new FlexQueryException(
                            $"Typed DTO mapping for '{kvp.Key}' is registered for DTO type '{kvp.Value.DtoType.Name}' " +
                            $"but the active response type is '{responseType.Name}'. " +
                            $"Ensure MapField<{responseType.Name}, {entityType.Name}, ...> is used.");

                    if (kvp.Value.EntityType != entityType)
                        throw new FlexQueryException(
                            $"Typed DTO mapping for '{kvp.Key}' is registered for entity type '{kvp.Value.EntityType.Name}' " +
                            $"but the active entity type is '{entityType.Name}'. " +
                            $"Ensure MapField<{responseType.Name}, {entityType.Name}, ...> is used.");

                    explicitMappings[kvp.Key] = kvp.Value;
                }
            }

            foreach (var dtoProp in responseProperties)
            {
                var dtoName = dtoProp.Name;

                if (registryTypeMap is not null
                    && registryTypeMap.TryResolveDestinationMember(dtoName, out var memberMap)
                    && !memberMap.IsImplicit)
                {
                    var sourceProp = memberMap.SourceProperty;
                    var computed = sourceProp is null;
                    var sourceValueType = memberMap.SourceValueType;

                    // Computed mappings (e.g. e => e.FirstName + " " + e.LastName) have no
                    // direct entity property — bind the DTO property and keep the computed
                    // entity expression. EntityProperty uses the first entity scalar as a
                    // synthetic placeholder for metadata only (excluded from entity-name
                    // resolution).
                    var entityProp = sourceProp ?? entityScalarProperties.FirstOrDefault()
                        ?? throw new FlexQueryException(
                            $"Computed mapping for '{dtoName}' requires at least one scalar entity property on '{entityType.Name}'.");

                    var field = new ResolvedField(
                        SurfaceName: dtoName,
                        ResponseProperty: dtoProp,
                        EntityProperty: entityProp,
                        EntityExpression: memberMap.SourceExpression,
                        MappingKind: FieldMappingKind.Explicit,
                        IsNavigation: memberMap.IsNavigation,
                        IsImplicitMapping: false,
                        SourceProperty: sourceProp,
                        SourceValueType: sourceValueType);

                    resolvableFields[dtoName] = field;
                    continue;
                }

                if (explicitMappings is { Count: > 0 } && explicitMappings.TryGetValue(dtoName, out var explicitMapping))
                {
                    var entityProp = explicitMapping.EntityProperty;
                    var entityExpr = explicitMapping.EntityExpression;

                    var field = new ResolvedField(
                        SurfaceName: dtoName,
                        ResponseProperty: dtoProp,
                        EntityProperty: entityProp,
                        EntityExpression: entityExpr,
                        MappingKind: FieldMappingKind.Explicit,
                        IsNavigation: !TypeClassification.IsScalarType(entityProp.PropertyType));

                    resolvableFields[dtoName] = field;
                    continue;
                }

                if (!entityPropsByName.TryGetValue(dtoName, out var matchingEntityProp)) continue;
                {
                    // Same-name convention:
                    // - Scalars require exact type equality.
                    // - Navigations require a registered nested entity → DTO type map for
                    //   the element/object types (List<OrderResponse> over List<Order>,
                    //   CustomerGroupResponse over CustomerGroup). Convention resolution
                    //   never silently maps raw entity graphs into DTO properties.
                    if (dtoProp.PropertyType != matchingEntityProp.PropertyType)
                    {
                        var dtoIsCollection = SafePropertyResolver.TryGetCollectionElementType(dtoProp.PropertyType, out var dtoElement);
                        var entityIsCollection = SafePropertyResolver.TryGetCollectionElementType(matchingEntityProp.PropertyType, out var entityElement);

                        if (!dtoIsCollection || !entityIsCollection)
                            continue;

                        // Element-wise convention requires a registered nested map for the
                        // element type pair (per-query registry falls through to the
                        // application-level registry).
                        var nestedMapRegistered =
                            (options.MappingRegistry?.Find(entityElement, dtoElement) is not null)
                            || (entityElement == dtoElement);

                        if (!nestedMapRegistered)
                            continue;
                    }

                    var isNavigation = !TypeClassification.IsScalarType(matchingEntityProp.PropertyType);

                    var param = Expression.Parameter(entityType, "x");
                    var body = Expression.Property(param, matchingEntityProp);
                    var lambda = Expression.Lambda(body, param);

                    var field = new ResolvedField(
                        SurfaceName: dtoName,
                        ResponseProperty: dtoProp,
                        EntityProperty: matchingEntityProp,
                        EntityExpression: lambda,
                        MappingKind: FieldMappingKind.Convention,
                        IsNavigation: isNavigation);

                    resolvableFields[dtoName] = field;
                }
            }

            // Populate ExpressionMappings on the request-local options instance
            // so the existing filter/sort/select pipeline can resolve DTO field names.
            options.ExpressionMappings ??= new Dictionary<string, LambdaExpression>(StringComparer.OrdinalIgnoreCase);

            foreach (var mapping in resolvableFields.Values)
            {
                options.ExpressionMappings[mapping.SurfaceName] = mapping.EntityExpression;
            }

            // Computed scalar mappings (MapField(alias, computedExpression)) are part of
            // the public query surface: they resolve like any other field for select,
            // filter, sort, and aggregate — and never require an include, because a
            // select node without children is not a navigation projection. Include
            // authorization remains structural (nested children ⇒ navigation projection).
            if (entityScalarProperties.Count <= 0)
                return new QuerySurface(
                    entityType,
                    responseType,
                    fieldNameComparer,
                    resolvableFields.Values.ToList(),
                    responseScalarProperties,
                    entityScalarProperties);
            {
                foreach (var mapping in options
                             .ExpressionMappings
                             .Where(mapping => !resolvableFields.ContainsKey(mapping.Key)))
                {
                    resolvableFields[mapping.Key] = new ResolvedField(
                        SurfaceName: mapping.Key,
                        ResponseProperty: null,
                        EntityProperty: entityScalarProperties[0],
                        EntityExpression: mapping.Value,
                        MappingKind: FieldMappingKind.Explicit,
                        IsNavigation: false,
                        IsImplicitMapping: false);
                }
            }
        }

        return new QuerySurface(
            entityType,
            responseType,
            fieldNameComparer,
            resolvableFields.Values.ToList(),
            responseScalarProperties,
            entityScalarProperties);
    }
}
