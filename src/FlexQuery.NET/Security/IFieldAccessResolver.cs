using FlexQuery.NET.Models;

namespace FlexQuery.NET.Security;

/// <summary>
/// Defines a contract for resolving field-level access permissions.
/// Allows for custom logic (e.g., role-based access) to be injected into the validation pipeline.
/// </summary>
public interface IFieldAccessResolver
{
    /// <summary>
    /// Determines whether access to the specified field path is allowed.
    /// </summary>
    /// <param name="fieldPath">The canonical path to the field (e.g., "Orders.Status").</param>
    /// <param name="context">The current query validation context.</param>
    /// <returns>True if allowed; otherwise, false.</returns>
    bool IsAllowed(string fieldPath, QueryContext context);
}
