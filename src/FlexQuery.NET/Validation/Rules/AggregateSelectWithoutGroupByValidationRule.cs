using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that aggregate queries without GROUP BY do not project entity fields.
/// When aggregates are used without grouping, the result is a single grand-total row;
/// projecting entity fields alongside aggregates is semantically undefined.
///
/// DTO-mode exemption: when a typed DTO surface is active, aggregate results live in
/// <c>QueryResult.Aggregates</c> metadata and row selections flow through the normal
/// DTO projection — the two output domains are separate, so a root select remains
/// valid alongside ungrouped aggregates.
/// </summary>
internal sealed class AggregateSelectWithoutGroupByValidationRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if (options.Aggregates.Count == 0) return;
        if (options.GroupBy is { Count: > 0 }) return;
        if (options.Select is not { Count: > 0 }) return;

        // DTO mode: row projection (QueryResult.Data) and aggregate metadata
        // (QueryResult.Aggregates) are separate output domains.
        if (context.QuerySurface?.ResponseType != null) return;

        result.Errors.Add(new ValidationError(
            "Entity fields cannot be selected when aggregates are used without GROUP BY.",
            ValidationErrorCodes.AggregateSelectWithoutGroupBy));
    }
}
