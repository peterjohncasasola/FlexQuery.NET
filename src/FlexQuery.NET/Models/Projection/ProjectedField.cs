namespace FlexQuery.NET.Models.Projection;

/// <summary>
/// Represents metadata about a single projected field.
/// </summary>
public sealed class ProjectedField
{
    /// <summary>
    /// The source property path (e.g., "Customer.Name" or "Orders.Total").
    /// </summary>
    public string SourcePath { get; internal set; } = null!;

    public string OutputName { get; internal set; } = null!;

    public Type FieldType { get; internal set; } = null!;

    public bool IsNavigation { get; internal set; }

    public int NavigationLevel { get; internal set; }

    public bool IsDeduplicated { get; internal set; }

    public string? Alias { get; internal set; }

    /// <summary>Factory method to create a <see cref="ProjectedField"/> instance.</summary>
    /// <param name="sourcePath">The source property path.</param>
    /// <param name="outputName">The output name (alias or original property name).</param>
    /// <param name="fieldType">The CLR type of the field.</param>
    /// <param name="isNavigation">Whether the field comes from a navigation property.</param>
    /// <param name="navigationLevel">Navigation level index (0 = root).</param>
    /// <param name="alias">Optional alias for the field.</param>
    /// <returns>A new <see cref="ProjectedField"/> instance.</returns>
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