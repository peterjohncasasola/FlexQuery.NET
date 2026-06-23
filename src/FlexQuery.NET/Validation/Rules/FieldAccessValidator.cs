using FlexQuery.NET.Models;
using FlexQuery.NET.Security;
using FlexQuery.NET.Exceptions;
using System.Collections.Concurrent;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Metadata;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// A comprehensive security validator that enforces field-level access control 
/// across filters, sorts, projections, group-by, aggregates, and having
/// using server-side execution rules.
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

        // 1. Default Projection: Auto-inject Select when none is specified.
        ApplyDefaultProjection(options, context, execOptions);

        // 2. Default Sort: Inject DefaultSortField when no sort is specified.
        ApplyDefaultSort(options, execOptions);

        // Validator self-skips if no security configuration is provided
        if (!IsSecurityActive(execOptions)) return;

        // 3. Process Filters - remove unauthorized in non-strict mode
        if (options.Filter != null)
        {
            ValidateFilterGroup(options.Filter, options, context, result, execOptions);
        }

        // 4. Process Sorts - remove unauthorized in non-strict mode
        if (options.Sort != null)
        {
            for (int i = options.Sort.Count - 1; i >= 0; i--)
            {
                var sort = options.Sort[i];
                if (!string.IsNullOrWhiteSpace(sort.Field))
                {
                    var accessField = ResolveSortAccessField(options, sort.Field);
                    if (!CheckAccess(accessField, QueryOperation.Sort, context, result))
                    {
                        if (!execOptions.StrictFieldValidation)
                        {
                            options.Sort.RemoveAt(i);
                        }
                    }
                }
            }
        }

        // 5. Process Selection - remove unauthorized in non-strict mode
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

        // 6. Process GroupBy - remove unauthorized in non-strict mode
        if (options.GroupBy != null)
        {
            for (int i = options.GroupBy.Count - 1; i >= 0; i--)
            {
                var field = options.GroupBy[i];
                if (!string.IsNullOrWhiteSpace(field))
                {
                    if (!CheckAccess(field, QueryOperation.Group, context, result))
                    {
                        if (!execOptions.StrictFieldValidation)
                        {
                            options.GroupBy.RemoveAt(i);
                        }
                    }
                }
            }
        }

        // 7. Process Aggregates - remove unauthorized in non-strict mode
        if (options.Aggregates != null)
        {
            for (int i = options.Aggregates.Count - 1; i >= 0; i--)
            {
                var aggregate = options.Aggregates[i];
                if (!string.IsNullOrWhiteSpace(aggregate.Field))
                {
                    if (!CheckAccess(aggregate.Field, QueryOperation.Aggregate, context, result))
                    {
                        if (!execOptions.StrictFieldValidation)
                        {
                            options.Aggregates.RemoveAt(i);
                        }
                    }
                }
            }
        }

        // 8. Process Having - check field if present
        if (options.Having != null && !string.IsNullOrWhiteSpace(options.Having.Field))
        {
            CheckAccess(options.Having.Field, QueryOperation.Having, context, result);
        }
    }

    private static void ApplyDefaultProjection(QueryOptions options, QueryContext ctx, QueryExecutionOptions execOptions)
    {
        if (options.Select != null && options.Select.Count > 0) return;
        if (options.SelectTree != null) return;
        if (options.HasProjection()) return;

        if (execOptions.SelectableFields?.Count > 0)
        {
            options.Select = execOptions.SelectableFields.ToList();
            return;
        }

        if (execOptions.AllowedFields?.Count > 0)
        {
            options.Select = execOptions.AllowedFields.ToList();
            return;
        }

        if (execOptions.BlockedFields?.Count > 0 && ctx?.TargetType != null)
        {
            var allScalars = ctx.TargetType
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(p => TypeClassification.IsScalarType(p.PropertyType))
                .Select(p => p.Name);
            options.Select = allScalars
                .Where(f => !WildcardMatcher.IsMatch(f, execOptions.BlockedFields))
                .ToList();
            return;
        }
    }

    private static void ApplyDefaultSort(QueryOptions options, QueryExecutionOptions execOptions)
    {
        if (string.IsNullOrWhiteSpace(execOptions.DefaultSortField)) return;
        if (options.Sort.Count > 0) return;

        options.Sort.Add(new SortNode
        {
            Field = execOptions.DefaultSortField,
            Descending = execOptions.DefaultSortDescending
        });
    }

    private static string ResolveSortAccessField(QueryOptions options, string sortField)
    {
        if (options.GroupBy is not { Count: > 0 })
        {
            return sortField;
        }

        var aggregate = options.Aggregates.FirstOrDefault(candidate =>
            candidate.Alias.Equals(sortField, StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrWhiteSpace(aggregate?.Field)
            ? sortField
            : aggregate.Field;
    }

    private bool IsSecurityActive(QueryExecutionOptions options)
    {
        return options.MaxFieldDepth.HasValue ||
               options.AllowedFields?.Count > 0 ||
               options.BlockedFields?.Count > 0 ||
               options.FilterableFields?.Count > 0 ||
               options.SortableFields?.Count > 0 ||
               options.SelectableFields?.Count > 0 ||
               options.GroupableFields?.Count > 0 ||
               options.AggregatableFields?.Count > 0 ||
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
            QueryOperation.Group => execOptions.GroupableFields,
            QueryOperation.Aggregate => execOptions.AggregatableFields,
            QueryOperation.Having => execOptions.AggregatableFields,
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

    /// <summary>
    /// Validates that the DefaultSortField is compatible with the configured governance rules.
    /// Call this at application startup to catch configuration errors early.
    /// Throws <see cref="QueryValidationException"/> if DefaultSortField violates governance.
    /// </summary>
    public static void ValidateDefaultSortFieldConfiguration(QueryExecutionOptions execOptions)
    {
        if (string.IsNullOrWhiteSpace(execOptions.DefaultSortField))
            return;

        var field = execOptions.DefaultSortField;

        // Check against BlockedFields
        if (execOptions.BlockedFields?.Count > 0)
        {
            if (WildcardMatcher.IsMatch(field, execOptions.BlockedFields))
            {
                throw new QueryValidationException(
                    $"DefaultSortField '{field}' is in BlockedFields and will always be rejected.");
            }
        }

        // Check against SortableFields
        if (execOptions.SortableFields?.Count > 0)
        {
            if (!WildcardMatcher.IsMatch(field, execOptions.SortableFields))
            {
                throw new QueryValidationException(
                    $"DefaultSortField '{field}' is not in SortableFields and will be rejected when sorting is validated.");
            }
        }

        // Check against AllowedFields (global whitelist)
        if (execOptions.AllowedFields?.Count > 0)
        {
            if (!WildcardMatcher.IsMatch(field, execOptions.AllowedFields))
            {
                throw new QueryValidationException(
                    $"DefaultSortField '{field}' is not in AllowedFields and will be rejected by the global allowlist.");
            }
        }
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
