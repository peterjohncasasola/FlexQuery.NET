using FlexQuery.NET.Caching;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
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
        if (options.SelectTree != null) return;
        if (options.HasProjection()) return;

        if (execOptions.SelectableFields?.Count > 0)
        {
            options.Select = ExpandWildcardFields(execOptions.SelectableFields, ctx.TargetType);
            return;
        }

        if (execOptions.RoleAllowedFields?.Count > 0 && !string.IsNullOrEmpty(execOptions.CurrentRole))
        {
            if (execOptions.RoleAllowedFields.TryGetValue(execOptions.CurrentRole, out var roleFields) && roleFields.Count > 0)
            {
                options.Select = ExpandWildcardFields(roleFields, ctx.TargetType);
                return;
            }
        }

        if (execOptions.AllowedFields?.Count > 0)
        {
            options.Select = ExpandWildcardFields(execOptions.AllowedFields, ctx.TargetType);
            return;
        }

        if (execOptions.BlockedFields?.Count > 0 && ctx.TargetType != null)
        {
            var allScalars = ReflectionCache.GetProperties(ctx.TargetType)
                .Where(p => TypeClassification.IsScalarType(p.PropertyType))
                .Select(p => p.Name);
            options.Select = allScalars
                .Where(f => !WildcardMatcher.IsMatch(f, execOptions.BlockedFields))
                .ToList();
        }
    }

    internal static List<string> ExpandWildcardFields(IEnumerable<string> fields, Type? targetType)
    {
        var result = new List<string>();
        foreach (var field in fields)
        {
            if (field.Contains('*'))
            {
                if (targetType == null)
                {
                    result.Add(field);
                    continue;
                }
                ExpandWildcard(field, targetType, result);
            }
            else
            {
                result.Add(field);
            }
        }
        return result;
    }

    private static void ExpandWildcard(string pattern, Type type, List<string> result)
    {
        var starIndex = pattern.IndexOf('*', StringComparison.Ordinal);
        if (starIndex < 0)
        {
            result.Add(pattern);
            return;
        }

        var pathBeforeStar = starIndex == 0
            ? string.Empty
            : pattern[..(starIndex - 1)];

        if (string.IsNullOrEmpty(pathBeforeStar))
        {
            ExpandUnderType(type, string.Empty, result);
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

            ExpandUnderType(currentType, string.Join(".", prefix), result);
        }
    }

    private static void ExpandUnderType(Type type, string prefix, List<string> result)
    {
        var allProps = ReflectionCache.GetProperties(type);

        foreach (var prop in allProps)
        {
            if (TypeClassification.IsScalarType(prop.PropertyType))
            {
                var fieldPath = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                result.Add(fieldPath);
            }
        }

        foreach (var prop in allProps)
        {
            if (!TypeClassification.IsScalarType(prop.PropertyType) && prop.PropertyType != typeof(object))
            {
                var childType = GetTargetType(prop.PropertyType);
                if (childType != null && childType != type)
                {
                    var childPrefix = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                    ExpandUnderType(childType, childPrefix, result);
                }
            }
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
