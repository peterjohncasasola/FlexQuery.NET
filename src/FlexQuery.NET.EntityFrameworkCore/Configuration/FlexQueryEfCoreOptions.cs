namespace FlexQuery.NET.EntityFrameworkCore.Configuration;

public sealed class FlexQueryEfCoreOptions
{
    public bool? UseNoTracking { get; set; }
    public bool? UseSplitQuery { get; set; }
    public bool? IgnoreAutoIncludes { get; set; }
    public bool? IgnoreQueryFilters { get; set; }
}
