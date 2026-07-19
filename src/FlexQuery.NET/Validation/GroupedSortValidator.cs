using FlexQuery.NET.Builders;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Paging;

namespace FlexQuery.NET.Validation;

/// <summary>
/// Validates and normalizes sort fields for grouped (GROUP BY) queries.
///
/// In a grouped query, only group-key fields and aggregate aliases are valid
/// sort targets. This validator resolves aggregate field names to aliases,
/// removes invalid sorts, and injects a deterministic fallback when no valid
/// sorts remain.
///
/// <para>Rules applied:</para>
/// <list type="bullet">
///   <item>Aggregate field names (e.g. <c>"Price"</c> when
///   <c>AVG(Price) AS priceAvg</c> exists) are resolved to their alias.</item>
///   <item>Invalid sorts (fields that are neither group keys nor aggregate aliases
///   or field names) throw a <see cref="QueryValidationException"/>.</item>
///   <item>If all input sorts are invalid or empty, a fallback sort by the first
///   group key ascending is injected to ensure deterministic paging.</item>
/// </list>
///
/// Both the EF Core and Dapper providers use this validator so that grouped
/// sort behavior is consistent across execution pipelines.
/// </summary>
internal static class GroupedSortValidator
{
    /// <summary>
    /// Validates the given sorts against the group-by fields and aggregates,
    /// returning a sanitized list that is safe to apply to a grouped query.
    /// </summary>
    /// <param name="sorts">The raw sort nodes from the query options.</param>
    /// <param name="groupByFields">The GROUP BY field paths.</param>
    /// <param name="aggregates">The aggregate projections.</param>
    /// <returns>
    /// A new list of <see cref="SortNode"/> values containing only valid sorts,
    /// with aggregate field names resolved to aliases, and guaranteed to have
    /// at least one entry when group-by fields exist.
    /// </returns>
    public static List<SortNode> Validate(
        IReadOnlyList<SortNode> sorts,
        IReadOnlyList<string> groupByFields,
        IReadOnlyList<Aggregate> aggregates)
    {
        var validFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fieldToAlias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in groupByFields)
        {
            var projectionName = GroupByBuilder.GetProjectionName(field);
            validFields.Add(projectionName);
        }

        foreach (var aggregate in aggregates)
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

        var result = new List<SortNode>(sorts.Count);
        foreach (var sort in sorts)
        {
            if (string.IsNullOrWhiteSpace(sort.Field)) continue;

            if (validFields.Contains(sort.Field))
            {
                result.Add(new SortNode
                {
                    Field = sort.Field,
                    Descending = sort.Descending
                });
                continue;
            }

            if (fieldToAlias.TryGetValue(sort.Field, out var alias))
            {
                result.Add(new SortNode
                {
                    Field = alias,
                    Descending = sort.Descending
                });
            }
        }

        if (result.Count == 0 && groupByFields.Count > 0)
        {
            result.Add(new SortNode
            {
                Field = GroupByBuilder.GetProjectionName(groupByFields[0]),
                Descending = false
            });
        }

        return result;
    }

    /// <summary>
    /// Validates the given sorts against the group-by fields and aggregates,
    /// returning a sanitized list. Unlike <see cref="Validate"/>, this method
    /// throws <see cref="QueryValidationException"/> if any sort field is invalid.
    /// </summary>
    /// <param name="sorts">The raw sort nodes from the query options.</param>
    /// <param name="groupByFields">The GROUP BY field paths.</param>
    /// <param name="aggregates">The aggregate projections.</param>
    /// <returns>A sanitized list of <see cref="SortNode"/> values.</returns>
    /// <exception cref="QueryValidationException">Thrown when a sort field is neither grouped nor aggregated.</exception>
    public static List<SortNode> ValidateOrThrow(
        IReadOnlyList<SortNode> sorts,
        IReadOnlyList<string> groupByFields,
        IReadOnlyList<Aggregate> aggregates)
    {
        var validFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fieldToAlias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in groupByFields)
        {
            var projectionName = GroupByBuilder.GetProjectionName(field);
            validFields.Add(projectionName);
        }

        foreach (var aggregate in aggregates)
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

        var invalidFields = new List<string>();
        var result = new List<SortNode>(sorts.Count);
        foreach (var sort in sorts)
        {
            if (string.IsNullOrWhiteSpace(sort.Field)) continue;

            if (validFields.Contains(sort.Field))
            {
                result.Add(new SortNode
                {
                    Field = sort.Field,
                    Descending = sort.Descending
                });
                continue;
            }

            if (fieldToAlias.TryGetValue(sort.Field, out var alias))
            {
                result.Add(new SortNode
                {
                    Field = alias,
                    Descending = sort.Descending
                });
                continue;
            }

            invalidFields.Add(sort.Field);
        }

        if (invalidFields.Count > 0)
        {
            var invalidList = string.Join(", ", invalidFields);
            var error = new ValidationError(
                $"Field(s) '{invalidList}' cannot be used for sorting because they are neither grouped nor aggregated.",
                ValidationErrorCodes.GroupBySortInvalid);
            var validationResult = new ValidationResult();
            validationResult.Errors.Add(error);
            throw new QueryValidationException(validationResult);
        }

        if (result.Count == 0 && groupByFields.Count > 0)
        {
            result.Add(new SortNode
            {
                Field = GroupByBuilder.GetProjectionName(groupByFields[0]),
                Descending = false
            });
        }

        return result;
    }
}

