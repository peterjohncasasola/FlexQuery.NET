using FlexQuery.NET.Models;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that all fields used in the query are permitted by the whitelist/blacklist.
/// </summary>
public sealed class FieldSecurityRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate<T>(QueryOptions options, ValidationResult result)
    {
        // If neither whitelist nor blacklist is provided, skip this rule.
        if (options.AllowedFields == null && options.BlockedFields == null)
        {
            return;
        }

        // 1. Validate Filters
        if (options.Filter != null)
        {
            ValidateFilterGroup(options.Filter, options, result, string.Empty, typeof(T));
        }

        // 2. Validate Sorts
        foreach (var sort in options.Sort)
        {
            if (string.IsNullOrWhiteSpace(sort.Field)) continue;
            
            var field = sort.Field;
            if (SafePropertyResolver.TryResolveChain(typeof(T), sort.Field, out var chain))
            {
                field = string.Join(".", chain.Select(p => p.Name));
            }
            CheckField(field, options, result, "sort");
        }

        // 3. Validate Selects
        if (options.Select != null)
        {
            foreach (var s in options.Select)
            {
                var field = s;
                if (SafePropertyResolver.TryResolveChain(typeof(T), s, out var chain))
                {
                    field = string.Join(".", chain.Select(p => p.Name));
                }
                CheckField(field, options, result, "select");
            }
        }
    }

    private void ValidateFilterGroup(FilterGroup group, QueryOptions options, ValidationResult result, string prefix, Type entityType)
    {
        foreach (var filter in group.Filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Field)) continue;

            string canonicalField = filter.Field;
            Type? nextType = null;

            if (SafePropertyResolver.TryResolveChain(entityType, filter.Field, out var chain))
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
            CheckField(fullPath, options, result, "filter");

            if (filter.ScopedFilter != null && nextType != null)
            {
                // Recursive check for scoped filters on collections
                ValidateFilterGroup(filter.ScopedFilter, options, result, fullPath, nextType);
            }
        }

        foreach (var subGroup in group.Groups)
        {
            ValidateFilterGroup(subGroup, options, result, prefix, entityType);
        }
    }

    private void CheckField(string fieldPath, QueryOptions options, ValidationResult result, string source)
    {
        // 1. Check Blacklist (BlockedFields)
        if (options.BlockedFields != null && options.BlockedFields.Contains(fieldPath))
        {
            result.Errors.Add(new ValidationError(
                $"Access to field '{fieldPath}' is blocked.",
                "FIELD_ACCESS_DENIED",
                fieldPath));
            return;
        }

        // 2. Check Whitelist (AllowedFields)
        if (options.AllowedFields != null && !options.AllowedFields.Contains(fieldPath))
        {
            result.Errors.Add(new ValidationError(
                $"Field '{fieldPath}' is not in the list of allowed fields.",
                "FIELD_ACCESS_DENIED",
                fieldPath));
        }
    }
}
