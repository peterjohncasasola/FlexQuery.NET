using FlexQuery.NET.Options;

namespace FlexQuery.NET.EntityFrameworkCore.Options;

/// <summary>
/// Represents Entity Framework Core-specific execution options for a
/// FlexQuery request.
/// </summary>
/// <inheritdoc/>
public sealed class EfCoreQueryOptions : QueryGovernanceOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EfCoreQueryOptions"/> class.
    /// </summary>
    /// <inheritdoc/>
    public EfCoreQueryOptions()
    {
        IncludeTotalCount = true;
        UseNoTracking = true;
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
}
