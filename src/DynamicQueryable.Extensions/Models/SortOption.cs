namespace DynamicQueryable.Models;

/// <summary>
/// Specifies a sort field and direction.
/// </summary>
public sealed class SortOption
{
    /// <summary>The property name to sort by.</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>If true, sorts descending; otherwise ascending.</summary>
    public bool Desc
    {
        get => Descending;
        set => Descending = value;
    }

    /// <summary>If true, sorts descending; otherwise ascending.</summary>
    public bool Descending { get; set; }
}
