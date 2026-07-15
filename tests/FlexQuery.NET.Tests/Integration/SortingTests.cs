using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Paging;

namespace FlexQuery.NET.Tests.Integration;

public class SortingTests : IDisposable
{
    private readonly TestDbContext _db = TestDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    // ── Single Sort ──────────────────────────────────────────────────────

    [Fact]
    public void Sort_SingleField_Ascending_OrdersCorrectly()
    {
        var opts = new QueryOptions
        {
            Sort   = [new SortNode { Field = "Age", Descending = false }],
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().Apply(opts).ToList();

        result.Should().BeInAscendingOrder(e => e.Age);
    }

    [Fact]
    public void Sort_SingleField_Descending_OrdersCorrectly()
    {
        var opts = new QueryOptions
        {
            Sort   = [new SortNode { Field = "Age", Descending = true }],
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().Apply(opts).ToList();

        result.Should().BeInDescendingOrder(e => e.Age);
    }

    [Fact]
    public void Sort_ByName_Ascending_OrdersAlphabetically()
    {
        var opts = new QueryOptions
        {
            Sort   = [new SortNode { Field = "Name", Descending = false }],
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().Apply(opts).ToList();

        result.Should().BeInAscendingOrder(e => e.Name);
    }

    [Fact]
    public void Sort_ByCreatedAt_Descending_LatestFirst()
    {
        var opts = new QueryOptions
        {
            Sort   = [new SortNode { Field = "CreatedAt", Descending = true }],
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().Apply(opts).ToList();

        result.Should().BeInDescendingOrder(e => e.CreatedAt);
    }

    // ── Multiple Sort ────────────────────────────────────────────────────

    [Fact]
    public void Sort_Multiple_CityAsc_ThenAgeDesc()
    {
        var opts = new QueryOptions
        {
            Sort =
            [
                new SortNode { Field = "City", Descending = false },
                new SortNode { Field = "Age",  Descending = true  }
            ],
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().Apply(opts).ToList();

        // Verify primary sort: cities are in ascending order
        var cities = result.Select(e => e.City).ToList();
        cities.Should().BeInAscendingOrder();

        // Verify secondary sort within same city group
        var berlinGroup = result.Where(e => e.City == "Berlin").ToList();
        berlinGroup.Should().BeInDescendingOrder(e => e.Age);
    }

    [Fact]
    public void Sort_Multiple_Fields_AllApplied()
    {
        var opts = new QueryOptions
        {
            Sort =
            [
                new SortNode { Field = "Status", Descending = false },
                new SortNode { Field = "Name",   Descending = false }
            ],
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().Apply(opts).ToList();

        result.Should().HaveCount(10);

        // Within each status group, names should be ascending
        var grouped = result.GroupBy(e => e.Status);
        foreach (var group in grouped)
            group.Select(e => e.Name).Should().BeInAscendingOrder();
    }

    [Fact]
    public void Sort_NestedProperty_ProfileBio_OrdersCorrectly()
    {
        var opts = new QueryOptions
        {
            Sort = [new SortNode { Field = "Profile.Bio", Descending = false }],
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().Apply(opts).ToList();

        result.Where(e => e.Profile is not null)
            .Select(e => e.Profile!.Bio)
            .Should().BeInAscendingOrder();
    }

    // ── Edge cases ───────────────────────────────────────────────────────

    [Fact]
    public void Sort_NoSortNodes_PreservesOriginalOrder()
    {
        var opts = new QueryOptions { Paging = { Disabled = true } };

        var result = _db.Entities.AsQueryable().Apply(opts).ToList();

        result.Should().HaveCount(10);
        result.Select(e => e.Id).Should().BeEquivalentTo(Enumerable.Range(1, 10));
    }

    [Fact]
    public void Sort_EmptyFieldName_IsSkippedGracefully()
    {
        var opts = new QueryOptions
        {
            Sort   = [new SortNode { Field = "", Descending = false }],
            Paging = { Disabled = true }
        };

        // Should not throw — empty field is skipped
        var act = () => _db.Entities.AsQueryable().Apply(opts).ToList();
        act.Should().NotThrow();
    }

    [Fact]
    public void Sort_InvalidProperty_IsIgnoredGracefully()
    {
        var opts = new QueryOptions
        {
            Sort =
            [
                new SortNode { Field = "NoSuchField", Descending = true },
                new SortNode { Field = "Age", Descending = false }
            ],
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().Apply(opts).ToList();

        result.Should().BeInAscendingOrder(e => e.Age);
    }

    [Fact]
    public void Sort_CollectionNavigation_IsIgnoredGracefully()
    {
        var opts = new QueryOptions
        {
            Sort =
            [
                new SortNode { Field = "Orders.Total", Descending = true },
                new SortNode { Field = "Id", Descending = false }
            ],
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().Apply(opts).ToList();

        result.Select(e => e.Id).Should().BeInAscendingOrder();
    }
}
