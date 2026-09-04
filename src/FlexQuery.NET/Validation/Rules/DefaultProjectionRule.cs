using FlexQuery.NET.Caching;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Metadata;
using FlexQuery.NET.Options;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Injects a default projection when the user has not specified explicit select fields.
/// Uses configured security options (SelectableFields, AllowedFields, BlockedFields)
/// to determine which fields to include by default.
/// </summary>
internal sealed class DefaultProjectionRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        var execOptions = context.ExecutionOptions;
        if (execOptions == null) return;

        DefaultProjectionHelper.InjectDefaultProjection(options, context, execOptions);
    }
}

internal static class DefaultProjectionHelper
{
    public static void InjectDefaultProjection(QueryOptions options, QueryContext ctx, QueryGovernanceOptions execOptions)
    {
        if (options.Select is { Count: > 0 }) return;
        if (options.HasProjection()) return;

        var hasDtoSurface = ctx.QuerySurface is { ResponseType: not null };

        // Build governance candidate if present
        List<string?>? governanceCandidate = null;
        if (execOptions.SelectableFields?.Count > 0)
        {
            governanceCandidate = ExpandWildcardFields(execOptions.SelectableFields, ctx.TargetType)
                .Select(n => n.Field)
                .ToList()!;
        }
        else if (execOptions.RoleAllowedFields?.Count > 0 && !string.IsNullOrEmpty(execOptions.CurrentRole))
        {
            if (execOptions.RoleAllowedFields.TryGetValue(execOptions.CurrentRole, out var roleFields) && roleFields.Count > 0)
            {
                governanceCandidate = ExpandWildcardFields(roleFields, ctx.TargetType)
                    .Select(n => n.Field)
                    .ToList();
            }
        }
        else if (execOptions.AllowedFields?.Count > 0)
        {
            governanceCandidate = ExpandWildcardFields(execOptions.AllowedFields, ctx.TargetType)
                .Select(n => n.Field)
                .ToList();
        }
        else if (execOptions.BlockedFields?.Count > 0 && ctx.TargetType != null)
        {
            var allScalars = ReflectionCache.GetProperties(ctx.TargetType)
                .Where(p => TypeClassification.IsScalarType(p.PropertyType))
                .Select(p => p.Name);
            governanceCandidate = allScalars
                .Where(f => !WildcardMatcher.IsMatch(f, execOptions.BlockedFields))
                .ToList()!;
        }

        if (hasDtoSurface)
        {
            var dtoDefaults = ctx.QuerySurface?.GetDefaultSelectFields();
            if (governanceCandidate != null)
            {
                // Intersect governance candidate with resolvable DTO fields
                if (dtoDefaults == null) return;
                
                var dtoDefaultSet = new HashSet<string>(dtoDefaults, ctx.QuerySurface?.FieldNameComparer);
                var finalFields = governanceCandidate
                    .Where(f => f != null && dtoDefaultSet.Contains(f))
                    .ToList();
                
                options.Select = finalFields.Select(f => new SelectNode { Field = f, IsSynthesized = true }).ToList();
            }
            else
            {
                // No governance default: use QuerySurface defaults directly
                if (dtoDefaults != null)
                    options.Select = dtoDefaults
                        .Select(f => new SelectNode { Field = f, IsSynthesized = true })
                        .ToList();
            }
            return;
        }

        // Non-DTO path: preserve existing behavior
        if (governanceCandidate != null)
        {
            options.Select = governanceCandidate.Select(f => new SelectNode { Field = f, IsSynthesized = true }).ToList();
            return;
        }

        if (execOptions.BlockedFields?.Count > 0 && ctx.TargetType != null)
        {
            var allScalars = ReflectionCache.GetProperties(ctx.TargetType)
                .Where(p => TypeClassification.IsScalarType(p.PropertyType))
                .Select(p => p.Name);
            options.Select = allScalars
                .Where(f => !WildcardMatcher.IsMatch(f, execOptions.BlockedFields))
                .Select(f => new SelectNode { Field = f, IsSynthesized = true })
                .ToList();
        }
    }

    internal static List<SelectNode> ExpandWildcardFields(IEnumerable<string> fields, Type? targetType)
    {
        var result = new List<SelectNode>();
        foreach (var field in fields)
        {
            if (field.Contains('*'))
            {
                if (targetType == null)
                {
                    result.Add(new SelectNode { Field = field, IsSynthesized = true });
                    continue;
                }
                ExpandWildcard(field, targetType, result);
            }
            else
            {
                result.Add(new SelectNode { Field = field, IsSynthesized = true });
            }
        }
        return result;
    }

    private static void ExpandWildcard(string pattern, Type type, List<SelectNode> result)
    {
        var starIndex = pattern.IndexOf('*', StringComparison.Ordinal);
        if (starIndex < 0)
        {
            result.Add(new SelectNode { Field = pattern, IsSynthesized = true });
            return;
        }

        var pathBeforeStar = starIndex == 0
            ? string.Empty
            : pattern[..(starIndex - 1)];

        var visited = new HashSet<Type>();
        if (string.IsNullOrEmpty(pathBeforeStar))
        {
            ExpandUnderType(type, string.Empty, result, visited);
        }
        else
        {
            var parts = pathBeforeStar.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var currentType = type;
            var prefix = new List<string>();

            foreach (var part in parts)
            {
                var prop = ReflectionCache.GetProperty(currentType, part);
                if (prop == null) return;
                prefix.Add(part);
                currentType = GetTargetType(prop.PropertyType);
                if (currentType == null) return;
            }

            ExpandUnderType(currentType, string.Join(".", prefix), result, visited);
        }
    }

    private static void ExpandUnderType(Type type, string prefix, List<SelectNode> result, HashSet<Type> visited)
    {
        if (!visited.Add(type))
            return;

        var allProps = ReflectionCache.GetProperties(type);

        foreach (var prop in allProps)
        {
            if (!TypeClassification.IsScalarType(prop.PropertyType)) continue;
            
            var fieldPath = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
            result.Add(new SelectNode { Field = fieldPath, IsSynthesized = true });
        }

        foreach (var prop in allProps)
        {
            if (TypeClassification.IsScalarType(prop.PropertyType) || prop.PropertyType == typeof(object)) continue;
            
            var childType = GetTargetType(prop.PropertyType);

            if (childType == null || childType == type) continue;
            
            var childPrefix = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
            ExpandUnderType(childType, childPrefix, result, visited);
        }
    }

    private static Type? GetTargetType(Type propertyType)
    {
        if (TypeClassification.IsCollectionType(propertyType, out var itemType))
            return itemType;
        if (propertyType.IsClass && propertyType != typeof(string))
            return propertyType;
        return null;
    }
}
