using FlexQuery.NET.Mapping;
using FlexQuery.NET.QuerySurface;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Metadata;

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
