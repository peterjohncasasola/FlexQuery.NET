using FlexQuery.NET.Models;
using FlexQuery.NET.Security;
using FlexQuery.NET.Exceptions;
using System.Collections.Concurrent;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// A comprehensive security validator that enforces field-level access control 
/// across filters, sorts, and projections using server-side execution rules.
/// </summary>
public sealed class FieldAccessValidator : IValidationRule
{
    private static readonly ConcurrentDictionary<string, string> _normalizationCache = new();

    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        var execOptions = context.ExecutionOptions;
        if (execOptions == null) return;

        // 1. Default Projection: If no select is requested but SelectableFields exist, use them.
        if ((options.Select == null || options.Select.Count == 0) && execOptions.SelectableFields?.Count > 0)
        {
            options.Select = execOptions.SelectableFields.ToList();
        }

        // Validator self-skips if no security configuration is provided
        if (!IsSecurityActive(execOptions)) return;

        try
        {
            // 2. Process Filters
            if (options.Filter != null)
            {
                ValidateFilterGroup(options.Filter, options, context);
            }

            // 3. Process Sorts
            if (options.Sort != null)
            {
                foreach (var sort in options.Sort)
                {
                    CheckAccess(sort.Field, QueryOperation.Sort, options, context);
                }
            }

            // 4. Process Selection
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
            // If strict validation is enabled, let the exception bubble up.
            if (execOptions.StrictFieldValidation)
            {
                throw;
            }

            // Extract field name from message if possible
            var field = "unknown";
            var parts = ex.Message.Split('\'');
            if (parts.Length >= 2) field = parts[1];

            result.Errors.Add(new ValidationError(ex.Message, "FIELD_ACCESS_DENIED", field));
        }
    }

    private bool IsSecurityActive(QueryExecutionOptions options)
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

    private void ValidateFilterGroup(FilterGroupNode group, QueryOptions options, QueryContext context, string? prefix = null)
    {
        foreach (var child in group.Children)
        {
            if (child is FilterConditionNode filter)
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
            else if (child is FilterGroupNode subGroup)
            {
                ValidateFilterGroup(subGroup, options, context, prefix);
            }
        }
    }

    private void CheckAccess(string rawField, QueryOperation operation, QueryOptions options, QueryContext context)
    {
        var execOptions = context.ExecutionOptions!;
        
        // Step 1: Normalize
        var field = NormalizeField(rawField, execOptions);

        // Step 2: Depth Validation
        if (execOptions.MaxFieldDepth.HasValue)
        {
            var depth = field.Split('.', StringSplitOptions.RemoveEmptyEntries).Length;
            if (depth > execOptions.MaxFieldDepth.Value)
            {
                throw new QueryValidationException($"Field path '{field}' exceeds maximum allowed depth of {execOptions.MaxFieldDepth}.");
            }
        }

        // Step 3: Resolver
        if (execOptions.FieldAccessResolver != null)
        {
            if (!execOptions.FieldAccessResolver.IsAllowed(field, operation, context))
            {
                throw new QueryValidationException($"Field '{field}' is not allowed for {operation} by custom resolver.");
            }
        }

        // Step 4: BlockedFields
        if (execOptions.BlockedFields != null && WildcardMatcher.IsMatch(field, execOptions.BlockedFields))
        {
            throw new QueryValidationException($"Field '{field}' is explicitly blocked.");
        }

        // Step 5: Role-Based
        if (execOptions.RoleAllowedFields != null && !string.IsNullOrEmpty(execOptions.CurrentRole))
        {
            if (execOptions.RoleAllowedFields.TryGetValue(execOptions.CurrentRole, out var allowedForRole))
            {
                if (!WildcardMatcher.IsMatch(field, allowedForRole))
                {
                    throw new QueryValidationException($"Field '{field}' is not allowed for role '{execOptions.CurrentRole}'.");
                }
            }
        }

        // Step 6: Operation-Level Rules
        HashSet<string>? opList = operation switch
        {
            QueryOperation.Filter => execOptions.FilterableFields,
            QueryOperation.Sort => execOptions.SortableFields,
            QueryOperation.Select => execOptions.SelectableFields,
            _ => null
        };

        if (opList != null)
        {
            if (!WildcardMatcher.IsMatch(field, opList))
            {
                throw new QueryValidationException($"Field '{field}' is not allowed for {operation} operation.");
            }
        }

        // Step 7: Global AllowedFields
        if (execOptions.AllowedFields != null)
        {
            if (!WildcardMatcher.IsMatch(field, execOptions.AllowedFields))
            {
                throw new QueryValidationException($"Field '{field}' is not in the global allowed list.");
            }
        }
    }

    private string NormalizeField(string rawField, QueryExecutionOptions options)
    {
        if (options.FieldMappings != null && options.FieldMappings.TryGetValue(rawField, out var mapped))
        {
            return mapped;
        }

        return _normalizationCache.GetOrAdd(rawField, f => f.Trim());
    }
}
