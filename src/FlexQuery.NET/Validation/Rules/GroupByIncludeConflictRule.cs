using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates that GROUP BY and relationship includes are not used together.
/// Navigation materialization across grouped results is semantically undefined
/// because group keys collapse multiple source rows into a single output
/// row, making per-group relationship inclusion ambiguous.
/// </summary>
internal sealed class GroupByIncludeConflictRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        if ((options.GroupBy?.Count ?? 0) == 0) return;

        var hasIncludes = (options.Includes?.Count ?? 0) > 0;

        if (hasIncludes)
        {
            result.Errors.Add(new ValidationError(
                "GROUP BY and Include cannot be combined. Navigation inclusion is not supported " +
                "with grouped queries. Remove GroupBy or remove all Include paths.",
                ValidationErrorCodes.GroupByIncludeConflict));
        }
    }
}
