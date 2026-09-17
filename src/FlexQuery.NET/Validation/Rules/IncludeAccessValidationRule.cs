using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Execution;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.Validation.Rules;

/// <summary>
/// Validates requested includes (every level of the include tree) against the
/// AllowedIncludes whitelist.
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

        // Check include tree paths - remove unauthorized ones in non-strict mode.
        // Each node's own full path is validated independently: an unauthorized
        // descendant must not deny its authorized parent or siblings.
        if (options.Includes is not null)
        {
            for (var i = options.Includes.Count - 1; i >= 0; i--)
            {
                if (!IsIncludePathAllowed(options.Includes[i], allowedIncludes, string.Empty, execOptions, result))
                {
                    // Lenient mode: remove only the unauthorized explicit node itself
                    // (its unauthorized subtree rides along with it).
                    options.Includes.RemoveAt(i);
                }
            }

            // Implicit ancestors whose only reason to exist was a now-removed
            // descendant are pruned (an explicit parent was never requested).
            PruneObsoleteScaffolding(options.Includes);
        }
    }

    private static void PruneObsoleteScaffolding(List<IncludeNode> nodes)
    {
        for (var i = nodes.Count - 1; i >= 0; i--)
        {
            var node = nodes[i];
            PruneObsoleteScaffolding(node.Children);

            if (!node.IsExplicitlyRequested
                && !node.HasOptions()
                && !ContainsExplicit(node))
            {
                nodes.RemoveAt(i);
            }
        }
    }

    private static bool ContainsExplicit(IncludeNode node)
        => node.Children.Any(c => c.IsExplicitlyRequested || ContainsExplicit(c));

    /// <summary>
    /// Validates one include node and its subtree against the whitelist.
    /// Returns false when the node is explicitly requested and unauthorized, so the
    /// caller must remove it (lenient mode). Authorized nodes with unauthorized
    /// descendants are kept: lenient mode prunes only the unauthorized children in
    /// place, and strict mode throws naming the exact unauthorized path.
    /// <para>
    /// Implicitly-synthesised intermediate ancestors (created while flattening a dotted
    /// path such as <c>orders.items</c>) are not themselves requested includes, so they
    /// are traversed for nested validation but never validated or pruned against the
    /// whitelist directly.
    /// </para>
    /// </summary>
    private static bool IsIncludePathAllowed(
        IncludeNode node,
        HashSet<string> allowedIncludes,
        string parentPath,
        QueryGovernanceOptions execOptions,
        ValidationResult result)
    {
        var fullPath = string.IsNullOrEmpty(parentPath) ? node.Path : $"{parentPath}.{node.Path}";

        if (node.IsExplicitlyRequested && !allowedIncludes.Contains(fullPath))
        {
            var message = $"Include path '{fullPath}' is not allowed.";
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
                if (!IsIncludePathAllowed(node.Children[i], allowedIncludes, fullPath, execOptions, result))
                {
                    node.Children.RemoveAt(i);
                }
            }
        }

        return true;
    }
}
