namespace FlexQuery.NET.EntityFrameworkCore.Configuration;

/// <summary>
/// Represents Entity Framework Core-specific execution options for a
/// FlexQuery request.
/// </summary>
public sealed class FlexQueryEfCoreOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether queries should be executed
    /// using <c>AsNoTracking()</c>.
    /// </summary>
    /// <remarks>
    /// When <see langword="null"/>, the default Entity Framework Core behaviour
    /// is used.
    /// </remarks>
    public bool? UseNoTracking { get; set; }
    
}