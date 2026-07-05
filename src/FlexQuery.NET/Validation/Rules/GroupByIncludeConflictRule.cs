using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that GROUP BY and Include/Expand are not used together.
/// Navigation expansion across grouped results is semantically undefined
/// because group keys collapse multiple source rows into a single output
/// row, making per-group navigation expansion ambiguous.
/// </summary>
public sealed class GroupByIncludeConflictRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if ((options.GroupBy?.Count ?? 0) == 0) return;

        var hasIncludes = (options.Includes?.Count ?? 0) > 0;
        var hasFilteredIncludes = (options.Expand?.Count ?? 0) > 0;

        if (hasIncludes || hasFilteredIncludes)
        {
            result.Errors.Add(new ValidationError(
                "GROUP BY and Include/Expand cannot be combined. Navigation expansion is not supported " +
                "with grouped queries. Remove GroupBy or remove all Include/Expand paths.",
                ValidationErrorCodes.GroupByIncludeConflict));
        }
    }
}
