using FlexQuery.NET.Models;
using FlexQuery.NET.Security;
using FlexQuery.NET.Exceptions;
using System.Collections.Concurrent;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// A comprehensive security validator that enforces field-level access control 
/// across filters, sorts, and projections.
/// </summary>
public sealed class FieldAccessValidator : IValidationRule
{
    private static readonly ConcurrentDictionary<string, string> _normalizationCache = new();

    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        // Validator self-skips if no security configuration is provided
        if (!IsSecurityActive(options)) return;

        try
        {
            // 1. Process Filters
            if (options.Filter != null)
            {
                ValidateFilterGroup(options.Filter, options, context);
            }

            // 2. Process Sorts
            if (options.Sort != null)
            {
                foreach (var sort in options.Sort)
                {
                    CheckAccess(sort.Field, QueryOperation.Sort, options, context);
                }
            }

            // 3. Process Selection
            if (options.Select != null)
            {
                foreach (var field in options.Select)
                {
                    CheckAccess(field, QueryOperation.Select, options, context);
                }
            }
        }
        catch (QueryValidationException ex)
        {
            // Extract field name from message if possible (format: Field 'name' is not allowed...)
            var field = "unknown";
            var parts = ex.Message.Split('\'');
            if (parts.Length >= 2) field = parts[1];

            result.Errors.Add(new ValidationError(ex.Message, "FIELD_ACCESS_DENIED", field));
        }
    }

    private bool IsSecurityActive(QueryOptions options)
    {
        return options.MaxFieldDepth.HasValue ||
               options.AllowedFields?.Count > 0 ||
               options.BlockedFields?.Count > 0 ||
               options.FilterableFields?.Count > 0 ||
               options.SortableFields?.Count > 0 ||
               options.SelectableFields?.Count > 0 ||
               options.RoleAllowedFields?.Count > 0 ||
               options.FieldAccessResolver != null;
    }

    private void ValidateFilterGroup(FilterGroup group, QueryOptions options, QueryContext context, string? prefix = null)
    {
        foreach (var filter in group.Filters)
        {
            var fieldPath = string.IsNullOrEmpty(prefix) ? filter.Field : $"{prefix}.{filter.Field}";

            if (!string.IsNullOrWhiteSpace(filter.Field))
            {
                CheckAccess(fieldPath, QueryOperation.Filter, options, context);
            }

            if (filter.ScopedFilter != null)
            {
                ValidateFilterGroup(filter.ScopedFilter, options, context, fieldPath);
            }
        }

        foreach (var subGroup in group.Groups)
        {
            ValidateFilterGroup(subGroup, options, context, prefix);
        }
    }

    private void CheckAccess(string rawField, QueryOperation operation, QueryOptions options, QueryContext context)
    {
        // Step 1: Normalize (Aliasing & Cache)
        var field = NormalizeField(rawField, options);

        // Step 2: Depth Validation
        if (options.MaxFieldDepth.HasValue)
        {
            var depth = field.Split('.', StringSplitOptions.RemoveEmptyEntries).Length;
            if (depth > options.MaxFieldDepth.Value)
            {
                throw new QueryValidationException($"Field path '{field}' exceeds maximum allowed depth of {options.MaxFieldDepth}.");
            }
        }

        // Step 3: Resolver (HIGHEST PRIORITY)
        if (options.FieldAccessResolver != null)
        {
            if (!options.FieldAccessResolver.IsAllowed(field, operation, context))
            {
                throw new QueryValidationException($"Field '{field}' is not allowed for {operation} by custom resolver.");
            }
        }

        // Step 4: BlockedFields (SECOND PRIORITY)
        if (options.BlockedFields != null && WildcardMatcher.IsMatch(field, options.BlockedFields))
        {
            throw new QueryValidationException($"Field '{field}' is explicitly blocked.");
        }

        // Step 5: Role-Based (THIRD PRIORITY)
        if (options.RoleAllowedFields != null && !string.IsNullOrEmpty(options.CurrentRole))
        {
            if (options.RoleAllowedFields.TryGetValue(options.CurrentRole, out var allowedForRole))
            {
                if (!WildcardMatcher.IsMatch(field, allowedForRole))
                {
                    throw new QueryValidationException($"Field '{field}' is not allowed for role '{options.CurrentRole}'.");
                }
            }
        }

        // Step 6: Operation-Level Rules (FOURTH PRIORITY)
        HashSet<string>? opList = operation switch
        {
            QueryOperation.Filter => options.FilterableFields,
            QueryOperation.Sort => options.SortableFields,
            QueryOperation.Select => options.SelectableFields,
            _ => null
        };

        if (opList != null)
        {
            if (!WildcardMatcher.IsMatch(field, opList))
            {
                throw new QueryValidationException($"Field '{field}' is not allowed for {operation} operation.");
            }
        }

        // Step 7: Global AllowedFields (FALLBACK)
        if (options.AllowedFields != null)
        {
            if (!WildcardMatcher.IsMatch(field, options.AllowedFields))
            {
                throw new QueryValidationException($"Field '{field}' is not in the global allowed list.");
            }
        }

        // Step 8: Default (Allow)
    }

    private string NormalizeField(string rawField, QueryOptions options)
    {
        // Try to resolve from mappings if provided
        if (options.FieldMappings != null && options.FieldMappings.TryGetValue(rawField, out var mapped))
        {
            return mapped;
        }

        return _normalizationCache.GetOrAdd(rawField, f => f.Trim());
    }
}
