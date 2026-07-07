using FlexQuery.NET.Options;

namespace FlexQuery.NET.EntityFrameworkCore.Options;


/// <inheritdoc/>
public sealed class EfCoreQueryOptions : BaseQueryOptions
{
    /// <inheritdoc/>
    public EfCoreQueryOptions()
    {
        IncludeTotalCount = true;
    }

    public bool? UseNoTracking { get; set; }
    public bool? UseSplitQuery { get; set; }
    public bool? IgnoreAutoIncludes { get; set; }
    public bool? IgnoreQueryFilters { get; set; }
}
