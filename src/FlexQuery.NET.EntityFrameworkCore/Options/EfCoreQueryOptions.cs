using FlexQuery.NET.Options;

namespace FlexQuery.NET.EntityFrameworkCore.Options;

/// <summary>
/// Represents Entity Framework Core-specific execution options for a
/// FlexQuery request.
/// </summary>
/// <inheritdoc/>
public sealed class EfCoreQueryOptions : BaseQueryOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EfCoreQueryOptions"/> class.
    /// </summary>
    /// <inheritdoc/>
    public EfCoreQueryOptions()
    {
        IncludeTotalCount = true;
    }

    /// <summary>
    /// Gets or sets a value indicating whether queries should be executed
    /// using <c>AsNoTracking()</c>.
    /// </summary>
    /// <remarks>
    /// When <see langword="null"/>, the Entity Framework Core default behavior
    /// is used.
    /// </remarks>
    public bool? UseNoTracking { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether collection navigations should
    /// be loaded using split queries.
    /// </summary>
    /// <remarks>
    /// When <see langword="null"/>, the Entity Framework Core default behavior
    /// is used.
    /// </remarks>
    public bool? UseSplitQuery { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether configured auto-includes should
    /// be ignored.
    /// </summary>
    /// <remarks>
    /// When <see langword="null"/>, the Entity Framework Core default behavior
    /// is used.
    /// </remarks>
    public bool? IgnoreAutoIncludes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether global query filters should
    /// be ignored.
    /// </summary>
    /// <remarks>
    /// When <see langword="null"/>, the Entity Framework Core default behavior
    /// is used.
    /// </remarks>
    public bool? IgnoreQueryFilters { get; set; }
}
