using DynamicQueryable.Extensions;
using DynamicQueryable.Models;
using DynamicQueryable.Tests.Fixtures;
using FluentAssertions;

namespace DynamicQueryable.Tests.Tests;

public class PagingTests : IDisposable
{
    private readonly TestDbContext _db = TestDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    // ── Valid paging ─────────────────────────────────────────────────────

    [Fact]
    public void Paging_FirstPage_ReturnsCorrectCount()
    {
        var opts = new QueryOptions { Paging = { Page = 1, PageSize = 3 } };

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().HaveCount(3);
    }

    [Fact]
    public void Paging_SecondPage_ReturnsNextBatch()
    {
        var opts1 = new QueryOptions { Paging = { Page = 1, PageSize = 3 } };
        var opts2 = new QueryOptions { Paging = { Page = 2, PageSize = 3 } };

        var page1 = _db.Entities.AsQueryable()
            .ApplySort(new QueryOptions { Sort = [new SortOption { Field = "Id" }] })
            .ApplyQueryOptions(opts1).ToList();

        var page2 = _db.Entities.AsQueryable()
            .ApplySort(new QueryOptions { Sort = [new SortOption { Field = "Id" }] })
            .ApplyQueryOptions(opts2).ToList();

        page1.Should().HaveCount(3);
        page2.Should().HaveCount(3);
        page1.Select(e => e.Id).Should().NotIntersectWith(page2.Select(e => e.Id));
    }

    [Fact]
    public void Paging_LastPage_ReturnsRemainingItems()
    {
        // 10 items, pageSize 3 → page 4 has 1 item
        var opts = new QueryOptions { Paging = { Page = 4, PageSize = 3 } };

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().HaveCount(1);
    }

    [Fact]
    public void Paging_Skip_IsCalculatedCorrectly()
    {
        // Page 3, size 4 → skip 8, take 4
        var paging = new PagingOptions { Page = 3, PageSize = 4 };
        paging.Skip.Should().Be(8);
        paging.PageSize.Should().Be(4);
    }

    // ── QueryResult metadata ─────────────────────────────────────────────

    [Fact]
    public void Paging_ToQueryResult_ReturnsCorrectMetadata()
    {
        var opts = new QueryOptions { Paging = { Page = 2, PageSize = 3 } };

        var result = _db.Entities.AsQueryable().ToQueryResult(opts);

        result.TotalCount.Should().Be(10);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(3);
        result.TotalPages.Should().Be(4);       // ceil(10/3)
        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeTrue();
        result.Data.Should().HaveCount(3);
    }

    [Fact]
    public void Paging_ToQueryResult_FirstPage_HasNoPreviousPage()
    {
        var opts = new QueryOptions { Paging = { Page = 1, PageSize = 5 } };

        var result = _db.Entities.AsQueryable().ToQueryResult(opts);

        result.HasPreviousPage.Should().BeFalse();
        result.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void Paging_ToQueryResult_LastPage_HasNoNextPage()
    {
        var opts = new QueryOptions { Paging = { Page = 2, PageSize = 5 } };

        var result = _db.Entities.AsQueryable().ToQueryResult(opts);

        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeTrue();
    }

    // ── Invalid paging fallback ──────────────────────────────────────────

    [Fact]
    public void Paging_ZeroPage_ClampsToOne()
    {
        var paging = new PagingOptions { Page = 0 };
        paging.Page.Should().Be(1);
        paging.Skip.Should().Be(0);
    }

    [Fact]
    public void Paging_NegativePage_ClampsToOne()
    {
        var paging = new PagingOptions { Page = -5 };
        paging.Page.Should().Be(1);
    }

    [Fact]
    public void Paging_ZeroPageSize_ClampsToOne()
    {
        var paging = new PagingOptions { PageSize = 0 };
        paging.PageSize.Should().Be(1);
    }

    [Fact]
    public void Paging_OversizedPageSize_ClampsTo1000()
    {
        var paging = new PagingOptions { PageSize = 99999 };
        paging.PageSize.Should().Be(1000);
    }

    [Fact]
    public void Paging_Disabled_ReturnsAllRecords()
    {
        var opts = new QueryOptions { Paging = { Disabled = true } };

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().HaveCount(10);
    }
}
