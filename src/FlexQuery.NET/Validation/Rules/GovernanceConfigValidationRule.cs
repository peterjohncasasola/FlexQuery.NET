using System.Collections.Concurrent;
using System.Text;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Metadata;
using FlexQuery.NET.Models;
using FlexQuery.NET.Options;
using FlexQuery.NET.Resolvers;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Safety net that verifies the configured governance lists actually reference members
/// that exist on the target entity type. Configuration is type-agnostic at application
/// startup, so a mistyped field (for example <c>"CustmerName"</c>) cannot be detected
/// until a query for the concrete type is executed. This rule performs that structural
/// check once per (entity type + governance configuration) and caches the outcome so it
/// does not add per-request overhead on the happy path.
/// </summary>
/// <remarks>
/// Wildcard entries (containing <c>*</c>) are skipped because they are patterns rather
/// than concrete members. Scalar/field lists are validated as property paths;
/// <see cref="QueryGovernanceOptions.AllowedIncludes"/> entries are validated as navigation paths.
/// Errors carry <see cref="ValidationErrorCodes.GovernanceFieldNotFound"/> so they are
/// clearly distinguishable from per-request field errors.
/// </remarks>
internal sealed class GovernanceConfigValidationRule : IValidationRule
{
    private static readonly ConcurrentDictionary<string, IReadOnlyList<ValidationError>> _cache = new();

    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        var execOptions = context.ExecutionOptions;
        var targetType = context.TargetType;
        if (execOptions == null || targetType == null) return;
        if (!HasGovernanceLists(execOptions)) return;

        var cacheKey = BuildCacheKey(targetType, execOptions);
        var errors = _cache.GetOrAdd(cacheKey, _ => Compute(targetType, execOptions));

        for (var i = 0; i < errors.Count; i++)
        {
            result.Errors.Add(errors[i]);
        }
    }

    private static bool HasGovernanceLists(QueryGovernanceOptions o)
        => o.AllowedFields?.Count > 0
           || o.BlockedFields?.Count > 0
           || o.SelectableFields?.Count > 0
           || o.FilterableFields?.Count > 0
           || o.SortableFields?.Count > 0
           || o.GroupableFields?.Count > 0
           || o.AggregatableFields?.Count > 0
           || o.AllowedIncludes?.Count > 0;

    private static IReadOnlyList<ValidationError> Compute(Type targetType, QueryGovernanceOptions execOptions)
    {
        var errors = new List<ValidationError>();

        ValidateFieldList(errors, targetType, execOptions, execOptions.AllowedFields, nameof(execOptions.AllowedFields));
        ValidateFieldList(errors, targetType, execOptions, execOptions.BlockedFields, nameof(execOptions.BlockedFields));
        ValidateFieldList(errors, targetType, execOptions, execOptions.SelectableFields, nameof(execOptions.SelectableFields));
        ValidateFieldList(errors, targetType, execOptions, execOptions.FilterableFields, nameof(execOptions.FilterableFields));
        ValidateFieldList(errors, targetType, execOptions, execOptions.SortableFields, nameof(execOptions.SortableFields));
        ValidateFieldList(errors, targetType, execOptions, execOptions.GroupableFields, nameof(execOptions.GroupableFields));
        ValidateFieldList(errors, targetType, execOptions, execOptions.AggregatableFields, nameof(execOptions.AggregatableFields));

        ValidateIncludeList(errors, targetType, execOptions.AllowedIncludes);

        return errors.Count == 0 ? Array.Empty<ValidationError>() : errors;
    }

    private static void ValidateFieldList(
        List<ValidationError> errors,
        Type targetType,
        QueryGovernanceOptions execOptions,
        HashSet<string>? list,
        string listName)
    {
        if (list is not { Count: > 0 }) return;

        foreach (var entry in list)
        {
            if (string.IsNullOrWhiteSpace(entry) || entry.Contains('*')) continue;

            if (!FieldResolver.TryResolveType(targetType, entry, execOptions, out _))
            {
                errors.Add(new ValidationError(
                    $"Invalid governance configuration. {listName} contains '{entry}', which does not exist on entity type '{targetType.Name}'.",
                    ValidationErrorCodes.GovernanceFieldNotFound,
                    entry));
            }
        }
    }

    private static void ValidateIncludeList(List<ValidationError> errors, Type targetType, HashSet<string>? includes)
    {
        if (includes is not { Count: > 0 }) return;

        foreach (var entry in includes)
        {
            if (string.IsNullOrWhiteSpace(entry) || entry.Contains('*')) continue;

            if (!SafePropertyResolver.TryResolveChain(targetType, entry, out var chain) || chain.Count == 0)
            {
                errors.Add(new ValidationError(
                    $"Invalid governance configuration. AllowedIncludes contains '{entry}', which does not exist on entity type '{targetType.Name}'.",
                    ValidationErrorCodes.GovernanceFieldNotFound,
                    entry));
                continue;
            }

            var last = chain[^1];
            var isNavigation = SafePropertyResolver.TryGetCollectionElementType(last.PropertyType, out _)
                               || (last.PropertyType.IsClass
                                   && last.PropertyType != typeof(string)
                                   && !TypeClassification.IsScalarType(last.PropertyType));

            if (!isNavigation)
            {
                errors.Add(new ValidationError(
                    $"Invalid governance configuration. AllowedIncludes contains '{entry}', but '{entry}' is not a navigation property on entity type '{targetType.Name}'.",
                    ValidationErrorCodes.GovernanceFieldNotFound,
                    entry));
            }
        }
    }

    private static string BuildCacheKey(Type targetType, QueryGovernanceOptions o)
    {
        var sb = new StringBuilder(targetType.FullName ?? targetType.Name);
        sb.Append(o.CaseInsensitive ? "|ci" : "|cs");
        Append(sb, "AF", o.AllowedFields);
        Append(sb, "BF", o.BlockedFields);
        Append(sb, "SelF", o.SelectableFields);
        Append(sb, "FF", o.FilterableFields);
        Append(sb, "SortF", o.SortableFields);
        Append(sb, "GF", o.GroupableFields);
        Append(sb, "AgF", o.AggregatableFields);
        Append(sb, "AI", o.AllowedIncludes);

        if (o.ExpressionMappings is { Count: > 0 })
        {
            sb.Append("|EM:");
            foreach (var key in o.ExpressionMappings.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                sb.Append(key).Append(',');
            }
        }

        return sb.ToString();
    }

    private static void Append(StringBuilder sb, string tag, HashSet<string>? list)
    {
        if (list is not { Count: > 0 }) return;
        sb.Append('|').Append(tag).Append(':');
        foreach (var entry in list.OrderBy(e => e, StringComparer.Ordinal))
        {
            sb.Append(entry).Append(',');
        }
    }
}
