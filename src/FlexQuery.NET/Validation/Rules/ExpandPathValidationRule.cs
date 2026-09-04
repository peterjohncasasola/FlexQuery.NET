using FlexQuery.NET.Caching;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Mapping;
using FlexQuery.NET.Metadata;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Options;
using FlexQuery.NET.QuerySurface;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates include and expand paths against the navigation graph, level by level.
/// In DTO mode, each navigation level resolves through its registered nested
/// entity → DTO type map (from the mapping registry, with global fallback), so nested
/// public DTO member names (e.g. <c>Orders.OrderStatus</c>) resolve correctly and
/// entity-only names are rejected at every level. Entity mode walks the entity graph
/// directly. All segments of a path must be navigation properties — a scalar segment
/// fails the walk.
/// </summary>
internal sealed class ExpandPathValidationRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        var type = context.TargetType;
        if (type == null) return;

        var surface = context.QuerySurface;
        var registry = context.ExecutionOptions?.MappingRegistry;
        var dtoType = surface?.ResponseType;

        if (options.Includes != null)
        {
            foreach (var include in options.Includes)
            {
                var walker = PathWalker.Start(type, dtoType, surface, registry);
                if (!walker.TryWalk(include, out var pathNotFound, out var notOnSurface))
                {
                    var code = pathNotFound ? ValidationErrorCodes.IncludePathNotFound : ValidationErrorCodes.NavigationPropertyRequired;
                    var message = notOnSurface && surface?.ResponseType != null
                        ? $"Include path '{include}' is not part of the public query surface for '{surface.ResponseType.Name}'. " +
                          "Expose the navigation on the DTO (same-name or MapField) to make it includable."
                        : pathNotFound
                            ? $"Include path '{include}' does not exist on type '{walker.CurrentTypeName()}'."
                            : $"Include path '{include}' contains one or more scalar properties. Only navigation properties are allowed.";

                    result.Errors.Add(new ValidationError(message, code, include));
                }
            }
        }

        if (options.Expand != null)
        {
            foreach (var node in options.Expand)
            {
                ValidateExpandNode(node, type, dtoType, surface, registry, string.Empty, result);
            }
        }
    }

    private static void ValidateExpandNode(
        IncludeNode node,
        Type entityType,
        Type? dtoType,
        IQuerySurface? surface,
        IQueryMappingRegistry? registry,
        string parentPath,
        ValidationResult result)
    {
        var fullPath = string.IsNullOrEmpty(parentPath) ? node.Path : $"{parentPath}.{node.Path}";

        var walker = PathWalker.Start(entityType, dtoType, surface, registry);
        if (!walker.TryWalk(node.Path, out var pathNotFound, out var notOnSurface))
        {
            var code = pathNotFound ? ValidationErrorCodes.IncludePathNotFound : ValidationErrorCodes.NavigationPropertyRequired;
            var message = notOnSurface && surface?.ResponseType != null && parentPath.Length == 0
                ? $"Expand path '{fullPath}' is not part of the public query surface for '{surface.ResponseType.Name}'. " +
                  "Expose the navigation on the DTO (same-name or MapField) to make it expandable."
                : pathNotFound
                    ? $"Expand path '{fullPath}' does not exist on type '{walker.CurrentTypeName()}'."
                    : $"Expand path '{fullPath}' contains one or more scalar properties. Only navigation properties are allowed.";

            result.Errors.Add(new ValidationError(message, code, fullPath));
            return;
        }

        var (childEntityType, childDtoType) = walker.CurrentTarget();

        if (node.Children.Count > 0)
        {
            foreach (var child in node.Children)
            {
                ValidateExpandNode(child, childEntityType, childDtoType, surface, registry, fullPath, result);
            }
        }
    }

    /// <summary>
    /// Level walker for navigation paths. Tracks the entity type and (in DTO mode) the
    /// DTO type at the current level; each segment resolves through the nested
    /// registered type map (DTO member names) or entity reflection (entity names).
    /// </summary>
    internal sealed class PathWalker
    {
        private Type _entityType;
        private Type? _dtoType;
        private readonly IQuerySurface? _surface;
        private readonly IQueryMappingRegistry? _registry;
        private ITypeMap? _typeMap;

        private PathWalker(Type entityType, Type? dtoType, IQuerySurface? surface, IQueryMappingRegistry? registry)
        {
            _entityType = entityType;
            _dtoType = dtoType;
            _surface = surface;
            _registry = registry;
            _typeMap = dtoType is null ? null : registry?.Find(entityType, dtoType);
        }

        public static PathWalker Start(Type entityType, Type? dtoType, IQuerySurface? surface, IQueryMappingRegistry? registry)
            => new(entityType, dtoType, surface, registry);

        /// <summary>The type name at the current walker level (for error messages).</summary>
        public string CurrentTypeName()
            => _dtoType is not null ? _dtoType.Name : _entityType.Name;

        /// <summary>Target types at the current level after a successful walk.</summary>
        public (Type EntityType, Type? DtoType) CurrentTarget()
            => (_entityType, _dtoType);

        /// <summary>
        /// Walks one dotted path from the current level. Every segment must resolve to a
        /// navigation property; a scalar segment fails the walk.
        /// </summary>
        public bool TryWalk(string path, out bool pathNotFound, out bool notOnSurface)
        {
            pathNotFound = false;
            notOnSurface = false;

            if (string.IsNullOrWhiteSpace(path))
            {
                pathNotFound = true;
                return false;
            }

            var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var segment in segments)
            {
                if (!TryWalkSegment(segment, out var entityPropType, out var nextDtoType, out var computedMapping))
                {
                    pathNotFound = !computedMapping;
                    notOnSurface = computedMapping;
                    return false;
                }

                if (!IsNavigationProperty(entityPropType))
                {
                    // Scalar segment — only navigation properties are walkable.
                    pathNotFound = false;
                    return false;
                }

                // Advance the walker to the navigation target, unwrapping collection
                // element types so the next segment resolves against the element level
                // (List<Order> → Order, List<OrderDto> → OrderDto).
                _entityType = UnwrapCollection(entityPropType);
                _dtoType = nextDtoType is null ? null : UnwrapCollection(nextDtoType);
                _typeMap = _dtoType is null ? null : _registry?.Find(_entityType, _dtoType);
            }

            return true;
        }

        private static Type UnwrapCollection(Type type)
            => SafePropertyResolver.TryGetCollectionElementType(type, out var element) && element is not null
                ? element
                : type;

        private bool TryWalkSegment(
            string segment,
            out Type entityPropType,
            out Type? nextDtoType,
            out bool computedMapping)
        {
            entityPropType = null!;
            nextDtoType = null;
            computedMapping = false;

            // DTO mode: resolve through the nested type map's mapped members (DTO member
            // names) and the DTO properties themselves. Entity-only names must not leak:
            // an entity property that the DTO surface does not expose is unresolvable.
            if (_typeMap is not null)
            {
                if (_typeMap.TryResolveDestinationMember(segment, out var memberMap))
                {
                    var sourceProp = memberMap.SourceProperty;
                    if (sourceProp is null)
                    {
                        // Computed navigation mapping — not entity-walkable.
                        computedMapping = true;
                        return false;
                    }

                    entityPropType = sourceProp.PropertyType;
                    nextDtoType = memberMap.DestinationValueType;
                    return true;
                }

                // The segment is not a mapped member — check the DTO-level property
                // (same-name convention on the DTO type was folded into the map at
                // creation; anything else is not part of the public surface).
                return false;
            }

            // Entity-mode level: resolve by entity property name.
            var prop = ReflectionCache.GetProperty(_entityType, segment);
            if (prop is null || !prop.CanRead)
                return false;

            entityPropType = prop.PropertyType;
            nextDtoType = null;
            return true;
        }

        private static bool IsNavigationProperty(Type propertyType)
        {
            if (SafePropertyResolver.TryGetCollectionElementType(propertyType, out _))
                return true;

            return propertyType.IsClass
                   && propertyType != typeof(string)
                   && !TypeClassification.IsScalarType(propertyType);
        }
    }
}
