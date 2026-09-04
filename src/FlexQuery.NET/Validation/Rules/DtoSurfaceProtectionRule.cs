using System.Collections;
using System.Reflection;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Mapping;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.QuerySurface;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Enforces the public query surface when a DTO response type is active
/// (<c>FlexQueryAsync&lt;TEntity, TResponse&gt;</c>).
///
/// Every operation — select, filter, sort, group, aggregate, and having — must resolve
/// its field through <see cref="IQuerySurface"/>. Entity-only property names (existing on
/// the entity but not exposed by the DTO) are rejected, regardless of any other
/// validation that would resolve them against the entity type.
///
/// In strict mode (default) this produces validation errors that throw
/// <see cref="Exceptions.QueryValidationException"/>; in lenient mode
/// (<c>StrictFieldValidation = false</c>) the offending fields are silently removed,
/// consistent with the existing lenient governance behavior.
/// </summary>
internal sealed class DtoSurfaceProtectionRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        var surface = context.QuerySurface;
        if (surface?.ResponseType == null) return;

        var strict = context.ExecutionOptions?.StrictFieldValidation ?? true;

        // 1. Select — only root-level fields are public; nested children are
        //    element-level fields validated against the navigation's element surface.
        if (options.Select is { Count: > 0 })
        {
            for (var i = options.Select.Count - 1; i >= 0; i--)
            {
                var node = options.Select[i];
                if (!IsPublicField(surface, node.Field))
                {
                    if (strict)
                        AddError(result, surface, node.Field);
                    else
                        options.Select.RemoveAt(i);
                    continue;
                }

                ValidateNestedSelectChildren(node, surface, context, result, strict);
            }
        }

        // 2. Filter
        if (options.Filter != null)
        {
            ValidateFilterGroup(options.Filter, surface, result, strict);
        }

        // 3. Sort
        for (var i = options.Sort.Count - 1; i >= 0; i--)
        {
            var sort = options.Sort[i];
            if (string.IsNullOrWhiteSpace(sort.Field)) continue;
            if (sort.Aggregate.HasValue) continue; // aggregate sorts validated below via aggregate fields
            if (IsGroupedAggregateAlias(options, sort.Field)) continue;

            if (!IsPublicField(surface, sort.Field))
            {
                if (strict)
                    AddError(result, surface, sort.Field);
                else
                    options.Sort.RemoveAt(i);
            }
        }

        // 4. Group
        if (options.GroupBy is { Count: > 0 })
        {
            for (var i = options.GroupBy.Count - 1; i >= 0; i--)
            {
                if (IsPublicField(surface, options.GroupBy[i])) continue;
                
                if (strict)
                    AddError(result, surface, options.GroupBy[i]);
                else
                    options.GroupBy.RemoveAt(i);
            }
        }

        // 5. Aggregate
        for (var i = options.Aggregates.Count - 1; i >= 0; i--)
        {
            var aggregate = options.Aggregates[i];
            if (string.IsNullOrWhiteSpace(aggregate.Field)) continue;

            if (IsPublicField(surface, aggregate.Field)) continue;
            if (strict)
                AddError(result, surface, aggregate.Field);
            else
                options.Aggregates.RemoveAt(i);
        }

        // 6. Having (errors only; lenient removal for having trees is not supported)
        if (options.Having != null)
        {
            ValidateHaving(options.Having, surface, result);
        }
    }

    private static bool IsPublicField(IQuerySurface surface, string? field)
    {
        if (string.IsNullOrWhiteSpace(field)) return true;

        var head = field.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
        return surface.TryResolve(head, out _);
    }

    /// <summary>
    /// Validates nested select children (e.g. <c>select=...,Orders(OrderId,Status)</c>)
    /// against the navigation element's public surface: every child must exist as a
    /// writable property on the element DTO type, resolve on the entity element type,
    /// or resolve as a mapped member of the registered nested entity → DTO type map
    /// (e.g. <c>Orders(OrderStatus)</c> when <c>OrderStatus</c> is explicitly mapped).
    /// Strict mode adds an error; lenient mode removes the child node.
    /// </summary>
    private static void ValidateNestedSelectChildren(SelectNode node, IQuerySurface surface, QueryContext context, ValidationResult result, bool strict)
    {
        if (node.Children is not { Count: > 0 }) return;
        if (!surface.TryResolve(node.Field, out var resolved)) return;

        var dtoNavType = resolved.ResponseProperty?.PropertyType;
        var entityNavType = resolved.EntityProperty.PropertyType;

        if (!TryGetElementOrSelf(dtoNavType, out var dtoElement)
            || !TryGetElementOrSelf(entityNavType, out var entityElement))
        {
            return;
        }

        var registry = context.ExecutionOptions?.MappingRegistry;
        var nestedMap = registry?.Find(entityElement, dtoElement);

        var dtoElementProps = dtoElement
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        var entityElementProps = entityElement
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        ValidateNestedChildren(node.Children, dtoElementProps, entityElementProps, dtoElement, result, strict, nestedMap, registry);

        if (!strict && node.Children.Count == 0)
        {
            node.Children.Clear();
        }
    }

    private static void ValidateNestedChildren(
        List<SelectNode> children,
        Dictionary<string, PropertyInfo> dtoElementProps,
        Dictionary<string, PropertyInfo> entityElementProps,
        Type dtoElement,
        ValidationResult result,
        bool strict,
        Mapping.ITypeMap? nestedMap,
        Mapping.IQueryMappingRegistry? registry)
    {
        for (var i = children.Count - 1; i >= 0; i--)
        {
            var child = children[i];
            if (string.IsNullOrWhiteSpace(child.Field) || child.Field == "*") continue;

            var onDto = dtoElementProps.TryGetValue(child.Field, out var dtoProp);
            var onEntity = entityElementProps.ContainsKey(child.Field);
            var onNestedMap = nestedMap is not null
                && nestedMap.TryResolveDestinationMember(child.Field, out _);

            if (!onDto || !(onEntity || onNestedMap))
            {
                if (strict)
                {
                    result.Errors.Add(new ValidationError(
                        $"Nested field '{child.Field}' is not part of the public query surface for '{dtoElement.Name}'. " +
                        "The entity model is an implementation detail; expose the field on the nested DTO or MapField.",
                        ValidationErrorCodes.FieldNotFound,
                        child.Field));
                }
                else
                {
                    children.RemoveAt(i);
                    continue;
                }
            }

            if (child.Children is not { Count: > 0 } || dtoProp is null) continue;

            if (!TryGetElementOrSelf(dtoProp.PropertyType, out var nestedDto)
                || !TryGetElementOrSelf(entityElementProps.TryGetValue(child.Field, out var entityPropValue)
                    ? entityPropValue.PropertyType
                    : dtoProp.PropertyType, out var nestedEntity)) continue;

            var nestedDtoProps = nestedDto
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            var nestedEntityProps = nestedEntity
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            var nestedChildMap = nestedMap is not null
                && nestedMap.TryResolveDestinationMember(child.Field, out var childMemberMap)
                && childMemberMap.IsNavigation
                ? registry?.Find(nestedEntity, nestedDto)
                : null;

            ValidateNestedChildren(child.Children, nestedDtoProps, nestedEntityProps, nestedDto, result, strict, nestedChildMap, registry);

            if (!strict && child.Children.Count == 0)
            {
                child.Children.Clear();
            }
        }
    }

    private static bool TryGetElementOrSelf(Type? type, out Type elementType)
    {
        if (type is null)
        {
            elementType = typeof(object);
            return false;
        }

        if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type))
        {
            elementType = type;
            return true;
        }

        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        var genericArgs = type.GetGenericArguments();
        if (genericArgs.Length == 1)
        {
            elementType = genericArgs[0];
            return true;
        }

        elementType = typeof(object);
        return false;
    }

    private static void ValidateFilterGroup(FilterGroup group, IQuerySurface surface, ValidationResult result, bool strict)
    {
        for (var i = group.Filters.Count - 1; i >= 0; i--)
        {
            var filter = group.Filters[i];
            if (IsPublicField(surface, filter.Field)) continue;
            if (strict)
                AddError(result, surface, filter.Field);
            else
            {
                group.Filters.RemoveAt(i);
            }
        }

        foreach (var subGroup in group.Groups)
        {
            ValidateFilterGroup(subGroup, surface, result, strict);
        }
    }

    private static void ValidateHaving(HavingNode having, IQuerySurface surface, ValidationResult result)
    {
        switch (having)
        {
            case HavingConditionNode c:
                if (!string.IsNullOrWhiteSpace(c.Field) && !IsPublicField(surface, c.Field))
                {
                    AddError(result, surface, c.Field);
                }
                break;
            case HavingLogicalNode l:
                foreach (var child in l.Children)
                    ValidateHaving(child, surface, result);
                break;
            case HavingGroupNode g:
                ValidateHaving(g.Inner, surface, result);
                break;
        }
    }

    private static bool IsGroupedAggregateAlias(QueryOptions options, string field)
        => options.GroupBy is { Count: > 0 }
           && options.Aggregates.Any(aggregate =>
               aggregate.Alias.Equals(field, StringComparison.OrdinalIgnoreCase));

    private static void AddError(ValidationResult result, IQuerySurface surface, string? field)
    {
        if (string.IsNullOrWhiteSpace(field)) return;

        result.Errors.Add(new ValidationError(
            $"Field '{field}' is not part of the public query surface for '{surface.ResponseType!.Name}'. " +
            "The entity model is an implementation detail; expose the field through the DTO or MapField.",
            ValidationErrorCodes.FieldNotFound,
            field));
    }
}

