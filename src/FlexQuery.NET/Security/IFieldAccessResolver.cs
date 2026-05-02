using FlexQuery.NET.Models;

namespace FlexQuery.NET.Security;

/// <summary>
/// Interface for custom logic to determine if a specific field is accessible for a given operation.
/// </summary>
public interface IFieldAccessResolver
{
    /// <summary>
    /// Determines if the specified field is allowed for the given operation and context.
    /// </summary>
    /// <param name="field">The normalized field path.</param>
    /// <param name="operation">The type of operation (Filter, Sort, Select).</param>
    /// <param name="context">The validation context containing metadata and target type.</param>
    /// <returns>True if access is allowed; otherwise, false.</returns>
    bool IsAllowed(string field, QueryOperation operation, QueryContext context);
}
