namespace FlexQuery.NET.AspNetCore.Attributes;

/// <summary>
/// Specifies field-level access permissions for a controller or action.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class FieldAccessAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the list of allowed fields (whitelist).
    /// </summary>
    public string[]? Allowed { get; set; }

    /// <summary>
    /// Gets or sets the list of blocked fields (blacklist).
    /// </summary>
    public string[]? Blocked { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed depth for nested field paths.
    /// </summary>
    public int MaxDepth { get; set; } = -1; // -1 means not set
}
