using FlexQuery.NET.Caching;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Security;
using FlexQuery.NET.Exceptions;
using System.Collections.Concurrent;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Metadata;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Options;

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
internal sealed class FieldAccessValidator : IValidationRule
{
    private static readonly ConcurrentDictionary<string, string> _normalizationCache = new();

    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        var execOptions = context.ExecutionOptions;
        if (execOptions == null) return;

        // Default Sort: Inject DefaultSortField when no sort is specified.
        ApplyDefaultSort(options, execOptions);

        // Validator self-skips if no security configuration is provided
        if (!IsSecurityActive(execOptions)) return;

        // 3. Process Filters - remove unauthorized in non-strict mode
        if (options.Filter != null)
        {
            ValidateFilterGroup(options.Filter, context, result, execOptions);
        }

        // 4. Process Sorts - remove unauthorized in non-strict mode
        for (var i = options.Sort.Count - 1; i >= 0; i--)
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

            // Re-apply default projection if all fields were removed in non-strict mode
            if (!execOptions.StrictFieldValidation && options.Select.Count == 0)
            {
                DefaultProjectionHelper.InjectDefaultProjection(options, context, execOptions);
            }
        }

        // 5b. Process SelectTree - remove unauthorized in non-strict mode
        if (options.SelectTree != null)
        {
            ValidateSelectTree(
                options.SelectTree,
                string.Empty,
                context.TargetType,
                context,
                result,
                execOptions);

            // Mirror flat-Select behavior: inject default projection when tree is emptied
            if (!execOptions.StrictFieldValidation &&
                options.SelectTree is { HasChildren: false, IncludeAllScalars: false })
            {
                options.SelectTree = null;
                DefaultProjectionHelper.InjectDefaultProjection(options, context, execOptions);
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
        for (var i = options.Aggregates.Count - 1; i >= 0; i--)
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

        // 8. Process Having - check field if present
        if (options.Having != null && !string.IsNullOrWhiteSpace(options.Having.Field))
        {
            CheckAccess(options.Having.Field, QueryOperation.Having, context, result);
        }
    }

    private static void ApplyDefaultSort(QueryOptions options, BaseQueryOptions execOptions)
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

    private void ValidateSelectTree(
        SelectionNode node,
        string prefix,
        Type? currentType,
        QueryContext context,
        ValidationResult result,
        BaseQueryOptions execOptions)
    {
        if (node.IncludeAllScalars && currentType != null)
        {
            var scalarFields = ReflectionCache.GetProperties(currentType)
                .Where(p => TypeClassification.IsScalarType(p.PropertyType))
                .Select(p => p.Name);

            if (execOptions.StrictFieldValidation)
            {
                foreach (var scalar in scalarFields)
                {
                    var fieldPath = string.IsNullOrEmpty(prefix) ? scalar : $"{prefix}.{scalar}";
                    CheckAccess(fieldPath, QueryOperation.Select, context, result);
                }
            }
            else
            {
                var allowedScalars = new List<string>();
                foreach (var scalar in scalarFields)
                {
                    var fieldPath = string.IsNullOrEmpty(prefix) ? scalar : $"{prefix}.{scalar}";
                    if (CheckAccess(fieldPath, QueryOperation.Select, context, result))
                    {
                        allowedScalars.Add(scalar);
                    }
                }

                node.ClearIncludeAllScalars();
                foreach (var scalar in allowedScalars)
                {
                    node.GetOrAddChild(scalar);
                }
            }
        }

        var children = node.EnumerateChildren().ToList();
        foreach (var kvp in children)
        {
            var childPath = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";
            var childType = ResolveSelectTreeChildType(currentType, kvp.Key);

            if (kvp.Value.HasChildren || kvp.Value.IncludeAllScalars)
            {
                ValidateSelectTree(kvp.Value, childPath, childType, context, result, execOptions);

                if (!execOptions.StrictFieldValidation && kvp.Value is { HasChildren: false, IncludeAllScalars: false })
                {
                    node.RemoveChild(kvp.Key);
                }
            }
            else
            {
                if (!CheckAccess(childPath, QueryOperation.Select, context, result))
                {
                    if (!execOptions.StrictFieldValidation)
                    {
                        node.RemoveChild(kvp.Key);
                    }
                }
            }
        }
    }

    private static Type? ResolveSelectTreeChildType(Type? parentType, string childKey)
    {
        if (parentType == null) return null;

        var prop = ReflectionCache.GetProperty(parentType, childKey);
        if (prop == null) return null;

        if (TypeClassification.IsCollectionType(prop.PropertyType, out var elementType))
            return elementType;
        if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
            return prop.PropertyType;
        return null;
    }

    private bool IsSecurityActive(BaseQueryOptions options)
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
        QueryContext context,
        ValidationResult result,
        BaseQueryOptions execOptions,
        string? prefix = null)
    {
        // Iterate backwards to allow removal during enumeration
        for (var i = group.Filters.Count - 1; i >= 0; i--)
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
                ValidateFilterGroup(filter.ScopedFilter, context, result, execOptions, fieldPath);
            }
        }

        // Process nested groups (backwards to allow removal)
        for (int i = group.Groups.Count - 1; i >= 0; i--)
        {
            ValidateFilterGroup(group.Groups[i], context, result, execOptions, prefix);
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
    public static void ValidateDefaultSortFieldConfiguration(BaseQueryOptions execOptions)
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
        BaseQueryOptions options,
        string field,
        string message)
    {
        if (options.StrictFieldValidation)
        {
            throw new QueryValidationException($"{message} (Field: {field})")
            {
                // Store the field in Result for consumers that inspect it
            };
        }

        result.Errors.Add(new ValidationError(message, ValidationErrorCodes.FieldAccessDenied, field));
    }

    private string NormalizeField(string rawField, BaseQueryOptions options)
    {
        if (options.FieldMappings != null && options.FieldMappings.TryGetValue(rawField, out var mapped))
        {
            return mapped;
        }
    
        return _normalizationCache.GetOrAdd(rawField, f => f.Trim());
    }
}
