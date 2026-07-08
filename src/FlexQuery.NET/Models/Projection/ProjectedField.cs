namespace FlexQuery.NET.Models.Projection;

/// <summary>
/// Represents metadata about a single projected field.
/// </summary>
public sealed class ProjectedField
{
    /// <summary>
    /// Gets the source property path (for example, <c>Customer.Name</c> or <c>Orders.Total</c>).
    /// </summary>
    public string SourcePath { get; internal set; } = null!;

    /// <summary>
    /// Gets the output field name after alias resolution.
    /// </summary>
    public string OutputName { get; internal set; } = null!;

    /// <summary>
    /// Gets the CLR type of the projected field.
    /// </summary>
    public Type FieldType { get; internal set; } = null!;

    /// <summary>
    /// Gets a value indicating whether the field originates from a navigation property.
    /// </summary>
    public bool IsNavigation { get; internal set; }

    /// <summary>
    /// Gets the navigation depth of the field, where <c>0</c> represents the root entity.
    /// </summary>
    public int NavigationLevel { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether the field was removed as a duplicate during
    /// projection normalization.
    /// </summary>
    public bool IsDeduplicated { get; internal set; }

    /// <summary>
    /// Gets the alias assigned to the field, if one was specified.
    /// </summary>
    public string? Alias { get; internal set; }

    /// <summary>
    /// Creates a new <see cref="ProjectedField"/> instance.
    /// </summary>
    /// <param name="sourcePath">The source property path.</param>
    /// <param name="outputName">The output field name (alias or original property name).</param>
    /// <param name="fieldType">The CLR type of the projected field.</param>
    /// <param name="isNavigation">Indicates whether the field originates from a navigation property.</param>
    /// <param name="navigationLevel">The navigation depth, where <c>0</c> represents the root entity.</param>
    /// <param name="alias">The optional alias assigned to the field.</param>
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