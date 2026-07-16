using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates requested includes against the AllowedIncludes whitelist.
/// </summary>
/// <remarks>
/// In non-strict mode (StrictFieldValidation = false), unauthorized includes
/// are silently removed from the query. In strict mode (default), validation
/// errors cause exceptions to be thrown.
/// </remarks>
internal sealed class IncludeAccessValidationRule : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        var execOptions = context.ExecutionOptions;
        if (execOptions?.AllowedIncludes is null || execOptions.AllowedIncludes.Count == 0)
        {
            return; // No include restrictions
        }

        var comparer = execOptions.CaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var allowedIncludes = new HashSet<string>(execOptions.AllowedIncludes, comparer);

        // Check flat includes - remove unauthorized ones in non-strict mode
        if (options.Includes is not null)
        {
            for (var i = options.Includes.Count - 1; i >= 0; i--)
            {
                var include = options.Includes[i];
                if (!allowedIncludes.Contains(include))
                {
                    var message = $"Include path '{include}' is not allowed.";
                    if (execOptions.StrictFieldValidation)
                    {
                        throw new QueryValidationException(message);
                    }
                    result.Errors.Add(new ValidationError(message, ValidationErrorCodes.IncludeAccessDenied, include));
                    // Remove in non-strict mode
                    options.Includes.RemoveAt(i);
                }
            }
        }
    }
}