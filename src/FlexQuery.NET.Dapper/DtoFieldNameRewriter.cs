using FlexQuery.NET.Mapping;
using FlexQuery.NET.QuerySurface;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Metadata;
using System.Reflection;

namespace FlexQuery.NET.Dapper;

/// <summary>
/// Rewrites DTO field names in <see cref="QueryOptions"/> to their underlying entity
/// property names using <see cref="IQuerySurface"/>. This allows the existing Dapper
/// SQL builders to operate unchanged, because they see entity field names.
/// Nested select children are rewritten through the registered nested
/// <see cref="ITypeMap"/> graph (CreateMap/ForMember), so renamed members — including
/// collection element fields such as <c>OrderResponse.DeliveryDate ←
/// Order.ExpectedDeliveryDate</c> — resolve to their entity property names.
/// </summary>
internal static class DtoFieldNameRewriter
{
    /// <summary>
    /// Rewrites DTO field names to entity field names in place for the typed DTO path.
    /// </summary>
    public static void Rewrite(QueryOptions options, IQuerySurface surface, IQueryMappingRegistry? mappingRegistry = null)
    {
        if (options.Select is { Count: > 0 })
        {
            for (var i = 0; i < options.Select.Count; i++)
            {
                options.Select[i] = RewriteSelectNode(options.Select[i], surface, mappingRegistry);
            }
        }

        for (var i = 0; i < options.Sort.Count; i++)
        {
            options.Sort[i] = RewriteSort(options.Sort[i], surface);
        }

        if (options.Filter != null)
        {
            options.Filter = RewriteFilterGroup(options.Filter, surface);
        }

        if (options.GroupBy is { Count: > 0 })
        {
            for (var i = 0; i < options.GroupBy.Count; i++)
            {
                options.GroupBy[i] = RewriteField(options.GroupBy[i], surface) ?? "";
            }
        }

        if (options.Aggregates is { Count: > 0 })
        {
            for (var i = 0; i < options.Aggregates.Count; i++)
            {
                options.Aggregates[i] = RewriteAggregate(options.Aggregates[i], surface);
            }
        }

        if (options.Having != null)
        {
            options.Having = RewriteHaving(options.Having, surface);
        }

        // Expand-block fields (filter/sort per navigation level) resolve against the
        // navigation element's TypeMap, not the root surface — rewrite them through the
        // registered nested map graph so ForMember renames like
        // OrderResponse.DeliveryDate ← Order.ExpectedDeliveryDate translate to entity
        // column names before SQL generation.
        if (options.Expand is { Count: > 0 })
        {
            RewriteExpandNodes(options.Expand, surface, mappingRegistry);
        }
    }

    private static void RewriteExpandNodes(
        List<IncludeNode> nodes,
        IQuerySurface surface,
        IQueryMappingRegistry? mappingRegistry,
        ITypeMap? parentMap = null,
        Type? parentEntityType = null)
    {
        foreach (var node in nodes)
        {
            // Each expand node's path may contain dotted segments ("Orders.OrderItems")
            // after TranslateIncludePathsToEntity resolves only the root segment — walk
            // segment-by-segment so every level's filter/sort resolves against its own
            // element TypeMap.
            RewriteExpandChain(
                node,
                node.Path,
                surface,
                mappingRegistry,
                parentMap,
                parentEntityType,
                walkedPath: string.Empty);
        }
    }

    private static void RewriteExpandChain(
        IncludeNode node,
        string remainingPath,
        IQuerySurface surface,
        IQueryMappingRegistry? mappingRegistry,
        ITypeMap? parentMap,
        Type? parentEntityType,
        string walkedPath)
    {
        if (mappingRegistry is null)
            return;

        if (string.IsNullOrEmpty(remainingPath))
        {
            // The chain for THIS node is exhausted; its filter/sort have been rewritten
            // by the last segment call. Descend into child nodes.
            if (node.Children is { Count: > 0 })
            {
                foreach (var child in node.Children)
                {
                    RewriteExpandChain(
                        child,
                        child.Path,
                        surface,
                        mappingRegistry,
                        parentMap,
                        parentEntityType,
                        walkedPath);
                }
            }
            return;
        }

        var dotIndex = remainingPath.IndexOf('.');
        var segment = dotIndex < 0 ? remainingPath : remainingPath[..dotIndex];
        var rest = dotIndex < 0 ? string.Empty : remainingPath[(dotIndex + 1)..];
        var fullPath = walkedPath.Length == 0 ? segment : $"{walkedPath}.{segment}";

        // Resolve this segment's member: on the parent map (DTO destination name first,
        // then raw entity property name), via reflection on the parent entity type when
        // the parent level is entity-typed (pass-through), or via the root surface at
        // the top level.
        PropertyMap? member = null;
        Type? entityElementType = null;
        Type? dtoElementType = null;

        if (parentMap is not null)
        {
            member = ResolveMemberOnMap(parentMap, segment);
            if (member is not null)
            {
                entityElementType = NavigationElementType(member.SourceValueType);
                dtoElementType = NavigationElementType(member.DestinationValueType);
            }
        }
        else if (parentEntityType is not null)
        {
            var navigation = parentEntityType.GetProperty(
                segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (navigation is null || !TryGetElementOrSelf(navigation.PropertyType, out var element))
                return;

            entityElementType = element;
            dtoElementType = element;   // pass-through level — no map metadata available
        }
        else if (surface.TryResolve(segment, out var resolved))
        {
            entityElementType = NavigationElementType(resolved.EntityProperty.PropertyType);
            dtoElementType = NavigationElementType(resolved.ResponseProperty?.PropertyType ?? resolved.EntityProperty.PropertyType);
        }

        if (entityElementType is null)
            return;

        var isNestedPair = dtoElementType is not null && entityElementType != dtoElementType;
        var elementMap = mappingRegistry.Find(entityElementType, isNestedPair ? dtoElementType! : entityElementType);

        if (rest.Length == 0)
        {
            // Last segment of this node's path: its filter/sort run against the
            // element entity type — rewrite through the element's registered map.
            if (node.Filter is not null)
                node.Filter = RewriteExpandFilter(node.Filter, fullPath, entityElementType, elementMap);

            if (node.Sort is { Count: > 0 })
            {
                for (var i = 0; i < node.Sort.Count; i++)
                {
                    node.Sort[i] = RewriteExpandSort(node.Sort[i], fullPath, entityElementType, elementMap);
                }
            }

            // Children start one level deeper (element entity type).
            foreach (var child in node.Children)
            {
                RewriteExpandChain(
                    child,
                    child.Path,
                    surface,
                    mappingRegistry,
                    elementMap,
                    entityElementType,
                    fullPath);
            }

            return;
        }

        // More segments remain in this node's path: descend into the element entity
        // type and keep walking.
        RewriteExpandChain(
            node,
            rest,
            surface,
            mappingRegistry,
            elementMap,
            entityElementType,
            fullPath);
    }

    private static PropertyMap? ResolveMemberOnMap(ITypeMap map, string segment)
    {
        if (map.TryResolveDestinationMember(segment, out var resolved))
            return resolved;

        var sourceProp = map.SourceType.GetProperty(
            segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (sourceProp is null)
            return null;

        return map.PropertyMaps.FirstOrDefault(m =>
            m.SourceProperty?.Name.Equals(sourceProp.Name, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static Type? NavigationElementType(Type type)
        => TryGetElementOrSelf(type, out var element) ? element : null;

    private static FilterGroup RewriteExpandFilter(FilterGroup group, string fullPath, Type entityElementType, ITypeMap? map)
    {
        var rewritten = new FilterGroup { Logic = group.Logic };
        foreach (var filter in group.Filters)
        {
            rewritten.Filters.Add(RewriteExpandFilterCondition(filter, fullPath, entityElementType, map));
        }
        foreach (var child in group.Groups)
        {
            rewritten.Groups.Add(RewriteExpandFilter(child, fullPath, entityElementType, map));
        }
        return rewritten;
    }

    private static FilterCondition RewriteExpandFilterCondition(FilterCondition condition, string fullPath, Type entityElementType, ITypeMap? map)
    {
        var rewritten = new FilterCondition
        {
            Field = condition.Field,
            Operator = condition.Operator,
            Value = condition.Value,
            ScopedFilter = condition.ScopedFilter is null
                ? null
                : RewriteExpandFilter(condition.ScopedFilter, fullPath, entityElementType, map)
        };

        // Scoped collection filters (Orders.Count(...)) switch target entity — leave
        // those fields for the scoped rewrite path.
        if (condition.ScopedFilter is not null)
            return rewritten;

        rewritten.Field = ResolveExpandField(condition.Field, fullPath, entityElementType, map);
        return rewritten;
    }

    private static SortNode RewriteExpandSort(SortNode node, string fullPath, Type entityElementType, ITypeMap? map)
    {
        var rewritten = new SortNode
        {
            Field = node.Field,
            Descending = node.Descending
        };

        if (!node.Aggregate.HasValue)
        {
            rewritten.Field = ResolveExpandField(node.Field, fullPath, entityElementType, map);
            return rewritten;
        }

        rewritten.Aggregate = node.Aggregate;
        rewritten.AggregateField = node.AggregateField;

        return rewritten;
    }

    private static string? ResolveExpandField(string? field, string fullPath, Type entityElementType, ITypeMap? map)
    {
        if (string.IsNullOrEmpty(field)) return field;

        // Renamed DTO members rewrite through the level's registered TypeMap.
        if (map is not null && map.TryResolveDestinationMember(field, out var member))
            return member.SourceProperty?.Name ?? field;

        // Entity-named fields pass through unchanged — but a field that exists on
        // neither the level's map nor its entity type is a request error; fail with a
        // precise message instead of letting an unknown column reach the database.
        if (entityElementType.GetProperty(
                field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) is null)
        {
            throw new FlexQueryException(
                $"Field '{field}' is not part of the expand surface for '{fullPath}'. " +
                "Expand filter and sort fields must belong to the expanded collection's element type " +
                "(using public DTO names when a CreateMap is registered for it).");
        }

        return field;
    }

    private static string? RewriteField(string? field, IQuerySurface surface)
    {
        if (string.IsNullOrEmpty(field) || !surface.TryResolve(field, out var resolved)) return field;
        // Computed scalar mappings (MapField(alias, computedExpression)) have no real
        // entity backing column — silently rewriting them to a placeholder column
        // would project the wrong data. They require a provider that can translate
        // the expression server-side (EF Core).
        if (resolved.ResponseProperty is null && resolved.MappingKind == FieldMappingKind.Explicit)
        {
            throw new FlexQueryException(
                $"Field '{field}' is a computed scalar mapping. Dapper v1 does not support " +
                "computed expressions in typed DTO projections because they cannot be " +
                "translated to SQL columns. Use a direct property mapping or the EF Core provider.");
        }

        return resolved.EntityProperty.Name;
    }

    private static SelectNode RewriteSelectNode(SelectNode node, IQuerySurface surface, IQueryMappingRegistry? mappingRegistry)
    {
        var dtoName = node.Field;
        var rewritten = new SelectNode
        {
            Field = RewriteField(dtoName, surface),
            Alias = node.Alias
        };

        // Navigation children project against the navigation's element surface, not the
        // root surface. Resolve the registered nested TypeMap (entity element → DTO
        // element) so child field names rewrite through ForMember metadata — the same
        // resolution the EF Core projection builder performs.
        if (node.Children is { Count: > 0 }
            && mappingRegistry is not null
            && surface.TryResolve(dtoName, out var parentField)
            && TryResolveNestedMap(parentField, mappingRegistry, out var nestedMap))
        {
            foreach (var child in node.Children)
            {
                rewritten.Children.Add(RewriteNestedChild(child, nestedMap, mappingRegistry));
            }

            return rewritten;
        }

        foreach (var child in node.Children)
        {
            rewritten.Children.Add(RewriteSelectNode(child, surface, mappingRegistry));
        }

        return rewritten;
    }

    /// <summary>
    /// Rewrites a select child under a navigation through its registered TypeMap.
    /// Direct property mappings (explicit ForMember and same-name convention) rewrite to
    /// the underlying entity property name; computed expressions have no single entity
    /// column and keep the public name. Deeper navigation children recurse through the
    /// member's own registered nested map.
    /// </summary>
    private static SelectNode RewriteNestedChild(SelectNode child, ITypeMap map, IQueryMappingRegistry registry)
    {
        var field = child.Field;

        if (map.TryResolveDestinationMember(child.Field ?? string.Empty, out var member))
        {
            // Direct property mappings (explicit ForMember and same-name convention)
            // project the underlying entity property; computed expressions have no single
            // entity column to project and keep the public name.
            if (member.SourceProperty is { } sourceProperty)
            {
                field = sourceProperty.Name;
            }

            if (child.Children is { Count: > 0 }
                && TryGetElementOrSelf(member.SourceValueType, out var childEntityElement)
                && TryGetElementOrSelf(member.DestinationValueType, out var childDtoElement)
                && childEntityElement != childDtoElement
                && registry.Find(childEntityElement, childDtoElement) is { } childMap)
            {
                var rewrittenBranch = new SelectNode { Field = field, Alias = child.Alias };
                foreach (var grandChild in child.Children)
                {
                    rewrittenBranch.Children.Add(RewriteNestedChild(grandChild, childMap, registry));
                }

                return rewrittenBranch;
            }
        }

        var rewritten = new SelectNode { Field = field, Alias = child.Alias };
        foreach (var grandChild in child.Children)
        {
            rewritten.Children.Add(grandChild);
        }

        return rewritten;
    }

    private static bool TryResolveNestedMap(
        ResolvedField field,
        IQueryMappingRegistry registry,
        out ITypeMap nestedMap)
    {
        nestedMap = null!;

        if (field.ResponseProperty is null)
            return false;

        if (!TryGetElementOrSelf(field.EntityProperty.PropertyType, out var entityElementType)
            || !TryGetElementOrSelf(field.ResponseProperty.PropertyType, out var dtoElementType))
            return false;

        if (entityElementType == dtoElementType)
            return false;

        nestedMap = registry.Find(entityElementType, dtoElementType)!;
        return nestedMap is not null;
    }

    private static bool TryGetElementOrSelf(Type type, out Type elementType)
    {
        if (type == typeof(string) || !typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
        {
            elementType = type;
            return true;
        }

        if (type.IsArray)
        {
            elementType = type.GetElementType() ?? type;
            return true;
        }

        var genericArguments = type.GetGenericArguments();
        if (genericArguments.Length == 1)
        {
            elementType = genericArguments[0];
            return true;
        }

        elementType = type;
        return false;
    }

    private static SortNode RewriteSort(SortNode node, IQuerySurface surface)
    {
        var rewritten = new SortNode
        {
            Field = RewriteField(node.Field, surface),
            Descending = node.Descending
        };

        if (!node.Aggregate.HasValue) return rewritten;
        
        rewritten.Aggregate = node.Aggregate;
        rewritten.AggregateField = node.AggregateField;

        return rewritten;
    }

    private static FilterGroup RewriteFilterGroup(FilterGroup group, IQuerySurface surface)
    {
        var rewritten = new FilterGroup { Logic = group.Logic };
        foreach (var filter in group.Filters)
        {
            rewritten.Filters.Add(RewriteFilterCondition(filter, surface));
        }
        foreach (var child in group.Groups)
        {
            rewritten.Groups.Add(RewriteFilterGroup(child, surface));
        }
        return rewritten;
    }

    private static FilterCondition RewriteFilterCondition(FilterCondition condition, IQuerySurface surface)
    {
        var rewritten = new FilterCondition
        {
            Field = RewriteField(condition.Field, surface),
            Operator = condition.Operator,
            Value = condition.Value,
            ScopedFilter = condition.ScopedFilter is null ? null : RewriteFilterGroup(condition.ScopedFilter, surface)
        };
        return rewritten;
    }

    private static Aggregate RewriteAggregate(Aggregate node, IQuerySurface surface)
    {
        return new Aggregate
        {
            Field = RewriteField(node.Field, surface),
            Function = node.Function,
            Alias = node.Alias
        };
    }

    private static HavingNode RewriteHaving(HavingNode having, IQuerySurface surface)
    {
        return having switch
        {
            HavingConditionNode c => new HavingConditionNode
            {
                Field = RewriteField(c.Field, surface),
                Operator = c.Operator,
                Value = c.Value
            },
            HavingLogicalNode l => new HavingLogicalNode
            {
                Logic = l.Logic,
                Children = l.Children.Select(child => RewriteHaving(child, surface)).ToList()
            },
            HavingGroupNode g => new HavingGroupNode
            {
                Inner = RewriteHaving(g.Inner, surface)
            },
            _ => having
        };
    }
}
