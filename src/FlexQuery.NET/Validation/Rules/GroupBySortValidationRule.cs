using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that sort fields in a grouped query reference only grouped fields
/// or aggregate aliases. Aggregate source field names are resolved to their
/// declared aliases before validation.
/// </summary>
internal sealed class GroupBySortValidationRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.GroupBy is not { Count: > 0 }) return;
        if (options.Sort is not { Count: > 0 }) return;

        var validFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fieldToAlias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in options.GroupBy)
        {
            validFields.Add(field);
        }

        foreach (var aggregate in options.Aggregates)
        {
            if (!string.IsNullOrEmpty(aggregate.Alias))
            {
                validFields.Add(aggregate.Alias);
                fieldToAlias[aggregate.Alias] = aggregate.Alias;
            }
            if (!string.IsNullOrEmpty(aggregate.Field))
            {
                fieldToAlias[aggregate.Field] = aggregate.Alias;
            }
        }

        foreach (var sort in options.Sort)
        {
            if (string.IsNullOrWhiteSpace(sort.Field)) continue;

            if (validFields.Contains(sort.Field)) continue;

            if (fieldToAlias.TryGetValue(sort.Field, out var alias))
            {
                continue;
            }

            result.Errors.Add(new ValidationError(
                $"Field '{sort.Field}' cannot be used for sorting because it is neither grouped nor aggregated.",
                ValidationErrorCodes.GroupBySortInvalid,
                sort.Field));
        }
    }
}
