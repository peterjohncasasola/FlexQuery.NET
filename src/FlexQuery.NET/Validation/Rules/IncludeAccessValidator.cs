using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Constants;
using System;
using System.Collections.Generic;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates requested includes against the AllowedIncludes whitelist.
/// </summary>
/// <remarks>
/// In non-strict mode (StrictFieldValidation = false), unauthorized includes
/// are silently removed from the query. In strict mode (default), validation
/// errors cause exceptions to be thrown.
/// </remarks>
public sealed class IncludeAccessValidator : IValidationRule
{
    /// <inheritdoc />
    public void Validate(QueryOptions options, QueryContext context, ValidationResult result)
    {
        var execOptions = context.ExecutionOptions;
        if (execOptions?.AllowedIncludes is null || execOptions.AllowedIncludes.Count == 0)
        {
            return; // No include restrictions
        }

        var comparer = execOptions.CaseInsensitiveFields ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var allowedIncludes = new HashSet<string>(execOptions.AllowedIncludes, comparer);

        // Check flat includes - remove unauthorized ones in non-strict mode
        if (options.Includes is not null)
        {
            for (int i = options.Includes.Count - 1; i >= 0; i--)
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

        // Check filtered includes - remove unauthorized ones in non-strict mode
        if (options.FilteredIncludes is not null)
        {
            for (int i = options.FilteredIncludes.Count - 1; i >= 0; i--)
            {
                var node = options.FilteredIncludes[i];
                if (ShouldRemoveIncludeNode(node, string.Empty, allowedIncludes, execOptions, result))
                {
                    options.FilteredIncludes.RemoveAt(i);
                }
            }
        }
    }

    private bool ShouldRemoveIncludeNode(
        IncludeNode node,
        string parentPath,
        HashSet<string> allowedIncludes,
        QueryExecutionOptions options,
        ValidationResult result)
    {
        var currentPath = string.IsNullOrEmpty(parentPath) ? node.Path : $"{parentPath}.{node.Path}";

        // Check if this node's path is allowed
        if (!allowedIncludes.Contains(currentPath))
        {
            var message = $"Include path '{currentPath}' is not allowed.";
            if (options.StrictFieldValidation)
            {
                throw new QueryValidationException(message);
            }
            result.Errors.Add(new ValidationError(message, ValidationErrorCodes.IncludeAccessDenied, currentPath));
            
            // Remove unauthorized child nodes
            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                ShouldRemoveIncludeNode(node.Children[i], currentPath, allowedIncludes, options, result);
            }
            
            return true; // Remove this node
        }

        // Check children recursively
        for (int i = node.Children.Count - 1; i >= 0; i--)
        {
            var child = node.Children[i];
            if (ShouldRemoveIncludeNode(child, currentPath, allowedIncludes, options, result))
            {
                node.Children.RemoveAt(i);
            }
        }

        return false;
    }
}