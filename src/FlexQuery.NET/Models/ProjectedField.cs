namespace FlexQuery.NET.Models;

/// <summary>
/// Represents metadata about a single projected field.
/// </summary>
public sealed class ProjectedField
{
    /// <summary>
    /// The source property path (e.g., "Customer.Name" or "Orders.Total").
    /// </summary>
    public string SourcePath { get; set; } = null!;

    /// <summary>
    /// The output name (alias or original property name).
    /// </summary>
    public string OutputName { get; set; } = null!;

    /// <summary>
    /// The CLR type of the projected field.
    /// </summary>
    public Type FieldType { get; set; } = null!;

    /// <summary>
    /// Whether this field comes from a navigation property.
    /// </summary>
    public bool IsNavigation { get; set; }

    /// <summary>
    /// Navigation level index (0 = root, 1 = first navigation, etc.) for flat projections.
    /// </summary>
    public int NavigationLevel { get; set; }

    /// <summary>
    /// Whether this field was deduplicated from another selection.
    /// </summary>
    public bool IsDeduplicated { get; set; }

    /// <summary>
    /// The alias if specified, otherwise null.
    /// </summary>
    public string? Alias { get; set; }

    public static ProjectedField Create(
        string sourcePath,
        string outputName,
        Type fieldType,
        bool isNavigation = false,
        int navigationLevel = 0,
        string? alias = null)
    {
        return new ProjectedField
        {
            SourcePath = sourcePath,
            OutputName = outputName,
            FieldType = fieldType,
            IsNavigation = isNavigation,
            NavigationLevel = navigationLevel,
            Alias = alias
        };
    }
}