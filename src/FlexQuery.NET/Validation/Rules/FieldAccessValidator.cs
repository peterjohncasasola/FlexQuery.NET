using FlexQuery.NET.Models;
using FlexQuery.NET.Security;
using FlexQuery.NET.Exceptions;
using System.Collections.Concurrent;
using FlexQuery.NET.Constants;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// A comprehensive security validator that enforces field-level access control 
/// across filters, sorts, and projections using server-side execution rules.
/// </summary>
/// <remarks>
/// In non-strict mode (StrictFieldValidation = false), unauthorized fields are
/// silently removed from the query. In strict mode (default), validation errors
/// cause exceptions to be thrown.
/// </remarks>
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

        // 2. Process Filters - remove unauthorized in non-strict mode
        if (options.Filter != null)
        {
            ValidateFilterGroup(options.Filter, options, context, result, execOptions);
        }

        // 3. Process Sorts - remove unauthorized in non-strict mode
        if (options.Sort != null)
        {
            for (int i = options.Sort.Count - 1; i >= 0; i--)
            {
                var sort = options.Sort[i];
                if (!string.IsNullOrWhiteSpace(sort.Field))
                {
                    if (!CheckAccess(sort.Field, QueryOperation.Sort, context, result))
                    {
                        if (!execOptions.StrictFieldValidation)
                        {
                            options.Sort.RemoveAt(i);
                        }
                    }
                }
            }
        }

        // 4. Process Selection - remove unauthorized in non-strict mode
        if (options.Select != null)
        {
            for (int i = options.Select.Count - 1; i >= 0; i--)
            {
                var field = options.Select[i];
                if (!string.IsNullOrWhiteSpace(field))
                {
                    if (!CheckAccess(field, QueryOperation.Select, context, result))
                    {
                        if (!execOptions.StrictFieldValidation)
                        {
                            options.Select.RemoveAt(i);
                        }
                    }
                }
            }
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

    private void ValidateFilterGroup(
        FilterGroup group,
        QueryOptions options,
        QueryContext context,
        ValidationResult result,
        QueryExecutionOptions execOptions,
        string? prefix = null)
    {
        // Iterate backwards to allow removal during enumeration
        for (int i = group.Filters.Count - 1; i >= 0; i--)
        {
            var filter = group.Filters[i];
            var fieldPath = string.IsNullOrEmpty(prefix) ? filter.Field : $"{prefix}.{filter.Field}";

            if (!string.IsNullOrWhiteSpace(filter.Field))
            {
                if (!CheckAccess(fieldPath, QueryOperation.Filter, context, result))
                {
                    if (!execOptions.StrictFieldValidation)
                    {
                        group.Filters.RemoveAt(i);
                    }
                    continue; // Skip scoped filter validation for removed condition
                }
            }

            if (filter.ScopedFilter != null)
            {
                ValidateFilterGroup(filter.ScopedFilter, options, context, result, execOptions, fieldPath);
            }
        }

        // Process nested groups (backwards to allow removal)
        for (int i = group.Groups.Count - 1; i >= 0; i--)
        {
            ValidateFilterGroup(group.Groups[i], options, context, result, execOptions, prefix);
        }
    }

    private bool CheckAccess(
        string rawField, 
        QueryOperation operation, 
        QueryContext context, 
        ValidationResult result)
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
                AddDenied(result, execOptions, field, $"Field path '{field}' exceeds maximum allowed depth of {execOptions.MaxFieldDepth}.");
                return false;
            }
        }

        // Step 3: Resolver
        if (execOptions.FieldAccessResolver != null)
        {
            if (!execOptions.FieldAccessResolver.IsAllowed(field, operation, context))
            {
                AddDenied(result, execOptions, field, $"Field '{field}' is not allowed for {operation} by custom resolver.");
                return false;
            }
        }

        // Step 4: BlockedFields
        if (execOptions.BlockedFields != null && WildcardMatcher.IsMatch(field, execOptions.BlockedFields))
        {
            AddDenied(result, execOptions, field, $"Field '{field}' is explicitly blocked.");
            return false;
        }

        // Step 5: Role-Based
        if (execOptions.RoleAllowedFields != null && !string.IsNullOrEmpty(execOptions.CurrentRole))
        {
            if (execOptions.RoleAllowedFields.TryGetValue(execOptions.CurrentRole, out var allowedForRole))
            {
                if (!WildcardMatcher.IsMatch(field, allowedForRole))
                {
                    AddDenied(result, execOptions, field, $"Field '{field}' is not allowed for role '{execOptions.CurrentRole}'.");
                    return false;
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
                AddDenied(result, execOptions, field, $"Field '{field}' is not allowed for {operation} operation.");
                return false;
            }
        }

        // Step 7: Global AllowedFields
        if (execOptions.AllowedFields != null)
        {
            if (!WildcardMatcher.IsMatch(field, execOptions.AllowedFields))
            {
                AddDenied(result, execOptions, field, $"Field '{field}' is not in the global allowed list.");
                return false;
            }
        }

        return true;
    }

    private static void AddDenied(
        ValidationResult result,
        QueryExecutionOptions options,
        string field,
        string message)
    {
        if (options.StrictFieldValidation)
        {
            throw new QueryValidationException(message);
        }

        result.Errors.Add(new ValidationError(message, ValidationErrorCodes.FieldAccessDenied, field));
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