namespace FlexQuery.NET.Models.Paging;

/// <summary>
/// Pagination parameters. All values are validated and clamped at usage time.
/// </summary>
public sealed class PagingOptions
{
    private const int MaxPageSize = 1000;
    private const int MinPageSize = 1;

    /// <summary>1-based current page number. Defaults to 1; clamped to >= 1.</summary>
    public int Page
    {
        get;
        set => field = value < 1 ? 1 : value;
    } = 1;

    /// <summary>Number of items per page. Defaults to 20; clamped to 1–1000.</summary>
    public int PageSize
    {
        get;
        set => field = value < MinPageSize ? MinPageSize : value > MaxPageSize ? MaxPageSize : value;
    } = 20;

    /// <summary>Whether paging is disabled (returns all records).</summary>
    public bool Disabled { get; set; }

    /// <summary>Computed zero-based skip count.</summary>
    public int Skip => (Page - 1) * PageSize;
}
