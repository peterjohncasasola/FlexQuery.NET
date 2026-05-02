using System.Text.RegularExpressions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that all fields used in the query are permitted based on whitelisting, blacklisting, and custom resolvers.
/// Supports nested paths, depth limiting, and wildcards (e.g., "Orders.*").
/// </summary>
public sealed class FieldAccessValidator : IValidationRule
{
    private static readonly Dictionary<string, Regex> RegexCache = new();

    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        // Skip validation if no security configuration is provided.
        if (options.AllowedFields == null && 
            options.BlockedFields == null && 
            options.FieldAccessResolver == null &&
            options.MaxFieldDepth == null)
        {
            return;
        }

        // 1. Validate Filters
        if (options.Filter != null)
        {
            ValidateFilterGroup(options.Filter, options, context, result, string.Empty);
        }

        // 2. Validate Sorts
        foreach (var sort in options.Sort)
        {
            if (string.IsNullOrWhiteSpace(sort.Field)) continue;
            
            var field = sort.Field;
            if (context.TargetType != null && SafePropertyResolver.TryResolveChain(context.TargetType, sort.Field, out var chain))
            {
                field = string.Join(".", chain.Select(p => p.Name));
            }
            CheckField(field, options, context, result);
        }

        // 3. Validate Selects
        if (options.Select != null)
        {
            foreach (var s in options.Select)
            {
                var field = s;
                if (context.TargetType != null && SafePropertyResolver.TryResolveChain(context.TargetType, s, out var chain))
                {
                    field = string.Join(".", chain.Select(p => p.Name));
                }
                CheckField(field, options, context, result);
            }
        }
    }

    private void ValidateFilterGroup(FilterGroup group, QueryOptions options, QueryContext context, ValidationResult result, string prefix)
    {
        foreach (var filter in group.Filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Field)) continue;

            string canonicalField = filter.Field;
            Type? nextType = null;

            if (context.TargetType != null && SafePropertyResolver.TryResolveChain(context.TargetType, filter.Field, out var chain))
            {
                canonicalField = chain[^1].Name;
                var lastProp = chain[^1];
                if (SafePropertyResolver.TryGetCollectionElementType(lastProp.PropertyType, out var elementType))
                {
                    nextType = elementType;
                }
                else
                {
                    nextType = lastProp.PropertyType;
                }
            }

            var fullPath = string.IsNullOrEmpty(prefix) ? canonicalField : $"{prefix}.{canonicalField}";
            CheckField(fullPath, options, context, result);

            if (filter.ScopedFilter != null)
            {
                // Recursive check for scoped filters
                ValidateFilterGroup(filter.ScopedFilter, options, context, result, fullPath);
            }
        }

        foreach (var subGroup in group.Groups)
        {
            ValidateFilterGroup(subGroup, options, context, result, prefix);
        }
    }

    private void CheckField(string fieldPath, QueryOptions options, QueryContext context, ValidationResult result)
    {
        // 1. Depth Limiting
        if (options.MaxFieldDepth.HasValue)
        {
            var depth = fieldPath.Split('.', StringSplitOptions.RemoveEmptyEntries).Length;
            if (depth > options.MaxFieldDepth.Value)
            {
                result.Errors.Add(new ValidationError(
                    $"Field path '{fieldPath}' exceeds the maximum allowed depth of {options.MaxFieldDepth.Value}.",
                    "FIELD_DEPTH_EXCEEDED",
                    fieldPath));
                return;
            }
        }

        // 2. Custom Resolver Priority
        if (options.FieldAccessResolver != null)
        {
            if (!options.FieldAccessResolver.IsAllowed(fieldPath, context))
            {
                AddError(fieldPath, result);
                return;
            }
        }

        // 3. Blacklist Check (Priority over whitelist)
        if (options.BlockedFields != null)
        {
            if (IsMatch(fieldPath, options.BlockedFields))
            {
                AddError(fieldPath, result);
                return;
            }
        }

        // 4. Whitelist Check
        if (options.AllowedFields != null)
        {
            if (!IsMatch(fieldPath, options.AllowedFields))
            {
                AddError(fieldPath, result);
            }
        }
    }

    private static bool IsMatch(string fieldPath, HashSet<string> patterns)
    {
        if (patterns.Contains(fieldPath)) return true;

        foreach (var pattern in patterns)
        {
            if (pattern.Contains('*'))
            {
                var regex = GetOrCreateRegex(pattern);
                if (regex.IsMatch(fieldPath)) return true;
            }
        }

        return false;
    }

    private static Regex GetOrCreateRegex(string pattern)
    {
        lock (RegexCache)
        {
            if (RegexCache.TryGetValue(pattern, out var regex)) return regex;

            // Convert wildcard pattern to regex
            // "Orders.*" -> "^Orders\..*$"
            // "*" -> "^.*$"
            var escaped = Regex.Escape(pattern).Replace("\\*", ".*");
            regex = new Regex($"^{escaped}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            RegexCache[pattern] = regex;
            return regex;
        }
    }

    private static void AddError(string fieldPath, ValidationResult result)
    {
        result.Errors.Add(new ValidationError(
            $"Field '{fieldPath}' is not allowed.",
            "FIELD_ACCESS_DENIED",
            fieldPath));
    }
}
