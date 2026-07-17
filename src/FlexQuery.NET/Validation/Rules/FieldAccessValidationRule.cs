using FlexQuery.NET.Caching;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Security;
using FlexQuery.NET.Exceptions;
using System.Collections.Concurrent;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Metadata;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Options;
using FlexQuery.NET.Models.Projection;

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
internal sealed class FieldAccessValidationRule : IValidationRule
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
            if (string.IsNullOrWhiteSpace(sort.Field)) continue;
            var accessField = ResolveSortAccessField(options, sort.Field);
            
            if (CheckAccess(accessField, QueryOperation.Sort, context, result)) continue;
            if (!execOptions.StrictFieldValidation)
            {
                options.Sort.RemoveAt(i);
            }
        }

        // 5. Process Selection - remove unauthorized in non-strict mode
        if (options.Select is { Count: > 0 })
        {
            ValidateSelect(options.Select, string.Empty, context.TargetType, context, result, execOptions);

            if (!execOptions.StrictFieldValidation && options.Select.Count == 0)
            {
                DefaultProjectionHelper.InjectDefaultProjection(options, context, execOptions);
            }
        }
        else if (options.SelectTree != null)
        {
            ValidateSelectTree(options.SelectTree, string.Empty, context.TargetType, context, result, execOptions);

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
            for (var i = options.GroupBy.Count - 1; i >= 0; i--)
            {
                var field = options.GroupBy[i];
                if (string.IsNullOrWhiteSpace(field)) continue;
                if (CheckAccess(field, QueryOperation.Group, context, result)) continue;
                if (!execOptions.StrictFieldValidation)
                {
                    options.GroupBy.RemoveAt(i);
                }
            }
        }

        // 7. Process Aggregates - remove unauthorized in non-strict mode
        for (var i = options.Aggregates.Count - 1; i >= 0; i--)
        {
            var aggregate = options.Aggregates[i];
            if (string.IsNullOrWhiteSpace(aggregate.Field)) continue;
            
            if (CheckAccess(aggregate.Field, QueryOperation.Aggregate, context, result)) continue;
            
            if (!execOptions.StrictFieldValidation)
            {
                options.Aggregates.RemoveAt(i);
            }
        }

        // 8. Process Having - check field if present
        if (options.Having != null && !string.IsNullOrWhiteSpace(options.Having.Field))
        {
            CheckAccess(options.Having.Field, QueryOperation.Having, context, result);
        }
    }

    private static void ApplyDefaultSort(QueryOptions options, QueryGovernanceOptions execOptions)
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

    private void ValidateSelect(
        List<SelectNode> siblings,
        string prefix,
        Type? currentType,
        QueryContext context,
        ValidationResult result,
        QueryGovernanceOptions execOptions)
    {
        for (var i = siblings.Count - 1; i >= 0; i--)
        {
            var node = siblings[i];
            var fieldPath = string.IsNullOrEmpty(prefix) ? node.Field : $"{prefix}.{node.Field}";

            var accessGranted = true;
            if (node.Field != "*")
            {
                accessGranted = CheckAccess(fieldPath, QueryOperation.Select, context, result);
            }

            if (!accessGranted && !execOptions.StrictFieldValidation)
            {
                siblings.RemoveAt(i);
                continue;
            }

            if (node.Children.Count <= 0) continue;
            
            var childType = ResolveSelectNodeChildType(currentType, node.Field);
            ValidateSelect(node.Children, fieldPath, childType, context, result, execOptions);

            if (!execOptions.StrictFieldValidation && node.Children.Count == 0)
            {
                node.Children.Clear();
            }
        }
    }

    private void ValidateSelectTree(
        SelectionNode node,
        string prefix,
        Type? currentType,
        QueryContext context,
        ValidationResult result,
        QueryGovernanceOptions execOptions)
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
            var childType = ResolveSelectNodeChildType(currentType, kvp.Key);

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
                if (CheckAccess(childPath, QueryOperation.Select, context, result)) continue;
                
                if (!execOptions.StrictFieldValidation)
                {
                    node.RemoveChild(kvp.Key);
                }
            }
        }
    }

    private static Type? ResolveSelectNodeChildType(Type? parentType, string childKey)
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

    private bool IsSecurityActive(QueryGovernanceOptions options)
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
        QueryGovernanceOptions execOptions,
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
        for (var i = group.Groups.Count - 1; i >= 0; i--)
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

        // Step 5b: Navigation traversal authorized by AllowedIncludes (additive).
        // For query-execution operations, a navigation whose path is present in
        // AllowedIncludes authorizes traversal into that navigation (e.g. filtering or
        // sorting on 'customerGroup.groupName' when 'customerGroup' is an allowed include),
        // even when the scalar-only operation lists or the global AllowedFields whitelist
        // would otherwise reject the dotted path. Blocked fields, role restrictions,
        // depth limits, and custom resolvers are enforced earlier and still take precedence.
        if (operation is QueryOperation.Filter or QueryOperation.Sort or QueryOperation.Group
                or QueryOperation.Aggregate or QueryOperation.Having
            && IsNavigationTraversalAllowedByIncludes(field, context, execOptions))
        {
            return true;
        }

        // Step 6: Operation-Level Rules
        var opList = operation switch
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
        if (execOptions.AllowedFields == null) return true;
        if (WildcardMatcher.IsMatch(field, execOptions.AllowedFields)) return true;
        
        AddDenied(result, execOptions, field, $"Field '{field}' is not in the global allowed list.");
        return false;

    }

    /// <summary>
    /// Determines whether a navigation-bearing field path is authorized for query
    /// execution (filter/sort/group/aggregate/having) because its navigation prefix
    /// is listed in <see cref="QueryGovernanceOptions.AllowedIncludes"/>.
    /// </summary>
    /// <remarks>
    /// This is the single navigation governance point described by the design:
    /// "includeable => traversable". A bare navigation (e.g. <c>Orders</c> used by a
    /// collection scoped filter) is authorized when it is itself an allowed include;
    /// a dotted path (e.g. <c>customerGroup.groupName</c>) is authorized when its
    /// navigation prefix (<c>customerGroup</c>) is an allowed include. The prefix must
    /// also resolve to an actual navigation on the target type.
    /// </remarks>
    private static bool IsNavigationTraversalAllowedByIncludes(
        string field,
        QueryContext context,
        QueryGovernanceOptions execOptions)
    {
        if (execOptions.AllowedIncludes is not { Count: > 0 }) return false;

        var targetType = context.TargetType;
        if (targetType == null) return false;

        var dotIndex = field.LastIndexOf('.');
        var navPath = dotIndex < 0 ? field : field[..dotIndex];
        if (string.IsNullOrEmpty(navPath)) return false;

        return IsIncludeListed(navPath, execOptions)
               && IsNavigationPath(targetType, navPath);
    }

    private static bool IsIncludeListed(string navPath, QueryGovernanceOptions execOptions)
    {
        var allowed = execOptions.AllowedIncludes!;
        var comparison = execOptions.CaseInsensitive
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        foreach (var include in allowed)
        {
            if (string.Equals(include, navPath, comparison))
                return true;
        }
        return false;
    }

    private static bool IsNavigationPath(Type rootType, string path)
    {
        if (!SafePropertyResolver.TryResolveChain(rootType, path, out var chain) || chain.Count == 0)
            return false;

        var last = chain[^1];
        if (SafePropertyResolver.TryGetCollectionElementType(last.PropertyType, out _))
            return true;

        var propertyType = last.PropertyType;
        return propertyType.IsClass
               && propertyType != typeof(string)
               && !TypeClassification.IsScalarType(propertyType);
    }

    /// <summary>
    /// Validates that the DefaultSortField is compatible with the configured governance rules.
    /// Call this at application startup to catch configuration errors early.
    /// Throws <see cref="QueryValidationException"/> if DefaultSortField violates governance.
    /// </summary>
    public static void ValidateDefaultSortFieldConfiguration(QueryGovernanceOptions execOptions)
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
        if (!(execOptions.AllowedFields?.Count > 0)) return;
        if (!WildcardMatcher.IsMatch(field, execOptions.AllowedFields))
        {
            throw new QueryValidationException(
                $"DefaultSortField '{field}' is not in AllowedFields and will be rejected by the global allowlist.");
        }
    }

    private static void AddDenied(
        ValidationResult result,
        QueryGovernanceOptions options,
        string field,
        string message)
    {
        var error = new ValidationError(message, ValidationErrorCodes.FieldAccessDenied, field);

        if (options.StrictFieldValidation)
        {
            // Carry the field-access error on the exception's Result so callers can
            // inspect the precise denial (code + field), consistent with the errors
            // accumulated in lenient mode.
            var strictResult = new ValidationResult();
            strictResult.Errors.Add(error);
            throw new QueryValidationException($"{message} (Field: {field})", strictResult);
        }

        result.Errors.Add(error);
    }

    private string NormalizeField(string rawField, QueryGovernanceOptions options)
    {
        if (options.FieldMappings != null && options.FieldMappings.TryGetValue(rawField, out var mapped))
        {
            return mapped;
        }
    
        return _normalizationCache.GetOrAdd(rawField, f => f.Trim());
    }
}
