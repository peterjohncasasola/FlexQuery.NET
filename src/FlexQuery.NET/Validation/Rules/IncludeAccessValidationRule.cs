using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
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

        var allowedIncludes = new HashSet<string>(execOptions.AllowedIncludes, StringComparer.OrdinalIgnoreCase);

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

        // Check expand paths - remove unauthorized ones in non-strict mode.
        // Each node's own full path is validated independently: an unauthorized
        // descendant must not deny its authorized parent or siblings.
        if (options.Expand is not null)
        {
            for (var i = options.Expand.Count - 1; i >= 0; i--)
            {
                if (!IsExpandPathAllowed(options.Expand[i], allowedIncludes, string.Empty, execOptions, result))
                {
                    // Lenient mode: remove only the unauthorized node itself.
                    options.Expand.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// Validates one expand node and its subtree against the whitelist.
    /// Returns false when the node's own path is unauthorized and the node must be
    /// removed (lenient mode). Authorized nodes with unauthorized descendants are kept:
    /// lenient mode prunes only the unauthorized children in place, and strict mode
    /// throws naming the exact unauthorized path.
    /// </summary>
    private static bool IsExpandPathAllowed(
        IncludeNode node,
        HashSet<string> allowedIncludes,
        string parentPath,
        QueryGovernanceOptions execOptions,
        ValidationResult result)
    {
        var fullPath = string.IsNullOrEmpty(parentPath) ? node.Path : $"{parentPath}.{node.Path}";

        if (!allowedIncludes.Contains(fullPath))
        {
            var message = $"Expand path '{fullPath}' is not allowed.";
            if (execOptions.StrictFieldValidation)
            {
                throw new QueryValidationException(message);
            }
            result.Errors.Add(new ValidationError(message, ValidationErrorCodes.IncludeAccessDenied, fullPath));
            return false;
        }

        if (node.Children is { Count: > 0 })
        {
            for (var i = node.Children.Count - 1; i >= 0; i--)
            {
                if (!IsExpandPathAllowed(node.Children[i], allowedIncludes, fullPath, execOptions, result))
                {
                    node.Children.RemoveAt(i);
                }
            }
        }

        return true;
    }
}