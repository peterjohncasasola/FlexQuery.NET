namespace DynamicQueryable.Models;

/// <summary>
/// Pagination parameters. All values are validated and clamped at usage time.
/// </summary>
public sealed class PagingOptions
{
    private int _page = 1;
    private int _pageSize = 20;
    private const int MAX_PAGE_SIZE = 1000;
    private const int MIN_PAGE_SIZE = 1;

    /// <summary>1-based current page number. Defaults to 1; clamped to >= 1.</summary>
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>Number of items per page. Defaults to 20; clamped to 1–1000.</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < MIN_PAGE_SIZE ? MIN_PAGE_SIZE : value > MAX_PAGE_SIZE ? MAX_PAGE_SIZE : value;
    }

    /// <summary>Whether paging is disabled (returns all records).</summary>
    public bool Disabled { get; set; }

    /// <summary>Computed zero-based skip count.</summary>
    public int Skip => (Page - 1) * PageSize;
}
