using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Validation.Rules;

internal sealed class PaginationModeValidationRule : IValidationRule
{
    public void Validate(
        QueryOptions options,
        QueryContext context,
        ValidationResult result)
    {
        if (!options.IsKeysetMode)
            return;

        if (options.OffsetExplicitlyRequested)
        {
            
            result.Errors.Add(new ValidationError("Offset pagination parameters cannot be used together with Keyset Pagination." +
                                                   "Choose either Offset Pagination or Keyset Pagination.", ValidationErrorCodes.PaginationModeConflict));
        }
    }
}