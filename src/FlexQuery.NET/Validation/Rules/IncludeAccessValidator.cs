using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Constants;
using System;
using System.Collections.Generic;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates requested includes against the AllowedIncludes whitelist.
/// </summary>
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

        try
        {
            // Check flat includes
            if (options.Includes is not null)
            {
                foreach (var include in options.Includes)
                {
                    CheckIncludeAccess(include, allowedIncludes);
                }
            }

            // Check filtered includes
            if (options.FilteredIncludes is not null)
            {
                foreach (var node in options.FilteredIncludes)
                {
                    ValidateIncludeNode(node, string.Empty, allowedIncludes);
                }
            }
        }
        catch (QueryValidationException ex)
        {
            if (execOptions.StrictFieldValidation)
            {
                throw;
            }

            // Extract include path from message if possible (format: "Include path '...' is not allowed.")
            var path = "unknown";
            var parts = ex.Message.Split('\'');
            if (parts.Length >= 2) path = parts[1];

            result.Errors.Add(new ValidationError(ex.Message, ValidationErrorCodes.IncludeAccessDenied, path));
        }
    }

    private void ValidateIncludeNode(IncludeNode node, string parentPath, HashSet<string> allowedIncludes)
    {
        var currentPath = string.IsNullOrEmpty(parentPath) ? node.Path : $"{parentPath}.{node.Path}";

        CheckIncludeAccess(currentPath, allowedIncludes);

        foreach (var child in node.Children)
        {
            ValidateIncludeNode(child, currentPath, allowedIncludes);
        }
    }

    private void CheckIncludeAccess(string path, HashSet<string> allowedIncludes)
    {
        // Exact match required; no wildcard evaluation per requirements.
        if (!allowedIncludes.Contains(path))
        {
            throw new QueryValidationException($"Include path '{path}' is not allowed.");
        }
    }
}
