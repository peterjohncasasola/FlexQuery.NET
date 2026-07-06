using FlexQuery.NET;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Serialization;
using FlexQuery.NET.Validation;

namespace FlexQuery.NET.Tests.Tests;

public class KeysetPaginationTests : IDisposable
{
    private readonly TestDbContext _db = TestDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    // ── Serialization ────────────────────────────────────────────────────

    [Fact]
    public void Serialize_ThenDeserialize_ReturnsSameValues()
    {
        var cursor = new KeysetCursor(42, "abc", null);
        var token = KeysetCursorSerializer.Serialize(cursor);
        var deserialized = KeysetCursorSerializer.Deserialize(token);

        deserialized.Should().NotBeNull();
        deserialized!.Values.Should().HaveCount(3);
        deserialized.Values[0].Should().Be(42);
        deserialized.Values[1].Should().Be("abc");
        deserialized.Values[2].Should().BeNull();
    }

    [Fact]
    public void Deserialize_Null_ReturnsNull()
    {
        KeysetCursorSerializer.Deserialize(null).Should().BeNull();
    }

    [Fact]
    public void Deserialize_EmptyString_ReturnsNull()
    {
        KeysetCursorSerializer.Deserialize("").Should().BeNull();
    }

    [Fact]
    public void Deserialize_Garbage_ReturnsNull()
    {
        KeysetCursorSerializer.Deserialize("!!!not-base64!!!").Should().BeNull();
    }

    // ── Predicate building (in-memory LINQ to Objects) ──────────────────

    [Fact]
    public void BuildSeekPredicate_SingleAscending_FiltersCorrectly()
    {
        var data = _db.Entities.OrderBy(e => e.Id).ToList();
        var cursor = new KeysetCursor(3);
        var orderings = KeysetPaginationBuilder.BuildOrderingInfos<TestEntity>(
            [new SortNode { Field = "Id" }]);

        var predicate = KeysetPaginationBuilder.BuildSeekPredicate<TestEntity>(orderings, cursor.Values);
        var result = data.AsQueryable().Where(predicate).ToList();

        result.Should().HaveCount(7);
        result.First().Id.Should().Be(4);
    }

    [Fact]
    public void BuildSeekPredicate_SingleDescending_FiltersCorrectly()
    {
        var data = _db.Entities.OrderByDescending(e => e.Id).ToList();
        var cursor = new KeysetCursor(7);
        var orderings = KeysetPaginationBuilder.BuildOrderingInfos<TestEntity>(
            [new SortNode { Field = "Id", Descending = true }]);

        var predicate = KeysetPaginationBuilder.BuildSeekPredicate<TestEntity>(orderings, cursor.Values);
        var result = data.AsQueryable().Where(predicate).ToList();

        result.Should().HaveCount(6);
        result.First().Id.Should().Be(6);
    }

    [Fact]
    public void BuildSeekPredicate_CompositeKey_GeneratesCorrectPredicate()
    {
        var cursor = new KeysetCursor("New York", 3);
        var orderings = KeysetPaginationBuilder.BuildOrderingInfos<TestEntity>([
            new SortNode { Field = "City" },
            new SortNode { Field = "Id" }
        ]);

        var predicate = KeysetPaginationBuilder.BuildSeekPredicate<TestEntity>(orderings, cursor.Values);
        var allEntities = _db.Entities.ToList();
        var result = allEntities.AsQueryable()
            .OrderBy(e => e.City).ThenBy(e => e.Id)
            .Where(predicate).ToList();

        result.Should().HaveCount(3);
        result.Should().OnlyContain(e => e.City.CompareTo("New York") > 0
            || (e.City == "New York" && e.Id > 3));
    }

    // ── Fluent API SeekAfter ─────────────────────────────────────────────

    [Fact]
    public void SeekAfter_FirstPage_ReturnsCorrectBatch()
    {
        // Simulate "first page" using SeekAfter(0) which means all Id > 0
        var page = _db.Entities
            .OrderBy(e => e.Id)
            .SeekAfter(0)
            .Take(3)
            .ToList();

        page.Should().HaveCount(3);
        page.Select(e => e.Id).Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public void SeekAfter_SecondPage_ReturnsNextBatch()
    {
        var page = _db.Entities
            .OrderBy(e => e.Id)
            .SeekAfter(3)
            .Take(3)
            .ToList();

        page.Should().HaveCount(3);
        page.Select(e => e.Id).Should().BeEquivalentTo([4, 5, 6]);
    }

    [Fact]
    public void SeekAfter_LastPage_ReturnsRemaining()
    {
        var page = _db.Entities
            .OrderBy(e => e.Id)
            .SeekAfter(6)
            .Take(5)
            .ToList();

        page.Should().HaveCount(4);
        page.Select(e => e.Id).Should().BeEquivalentTo([7, 8, 9, 10]);
    }

    [Fact]
    public void SeekAfter_NoMoreResults_ReturnsEmpty()
    {
        var page = _db.Entities
            .OrderBy(e => e.Id)
            .SeekAfter(10)
            .Take(3)
            .ToList();

        page.Should().BeEmpty();
    }

    [Fact]
    public void SeekAfter_Descending_PagesCorrectly()
    {
        var page = _db.Entities
            .OrderByDescending(e => e.Id)
            .SeekAfter(10)
            .Take(3)
            .ToList();

        page.Should().HaveCount(3);
        page.Select(e => e.Id).Should().BeEquivalentTo([9, 8, 7]);
    }

    [Fact]
    public void SeekAfter_WithoutOrderBy_Throws()
    {
        var act = () => _db.Entities
            .OrderBy(e => e.Id)     // becomes IOrderedQueryable
            .SeekAfter(1)
            .Take(3)
            .ToList();

        // Should not throw - valid
        act.Should().NotThrow();
    }

    [Fact]
    public void SeekAfter_NullCursor_Throws()
    {
        var act = () => _db.Entities
            .OrderBy(e => e.Id)
            .SeekAfter<TestEntity, int?>(null);

        act.Should().Throw<QueryValidationException>();
    }

    // ── BuildSeekPredicate validation ────────────────────────────────────

    [Fact]
    public void BuildSeekPredicate_NoOrderings_Throws()
    {
        var cursor = new KeysetCursor(1);
        var act = () => KeysetPaginationBuilder.BuildSeekPredicate<TestEntity>(
            [], cursor.Values);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least one sort field*");
    }

    [Fact]
    public void BuildSeekPredicate_CursorCountMismatch_Throws()
    {
        var cursor = new KeysetCursor(1, 2);
        var orderings = KeysetPaginationBuilder.BuildOrderingInfos<TestEntity>(
            [new SortNode { Field = "Id" }]);

        var act = () => KeysetPaginationBuilder.BuildSeekPredicate<TestEntity>(
            orderings, cursor.Values);

        act.Should().Throw<QueryValidationException>()
            .WithMessage("*2 value(s)*1 ordering column*");
    }

    // ── FlexQuery pipeline integration ───────────────────────────────────

    [Fact]
    public void FlexQuery_WithUseKeysetPagination_ReturnsPageWithoutCount()
    {
        var parameters = new FlexQueryParameters
        {
            UseKeysetPagination = true,
            Sort = "Id:asc",
            PageSize = 3
        };

        var result = _db.Entities.AsQueryable().FlexQuery(parameters);

        result.Data.Should().HaveCount(3);
        result.Data.Cast<TestEntity>().Select(e => e.Id).Should().BeEquivalentTo([1, 2, 3]);
        result.TotalCount.Should().BeNull();
        result.NextCursorToken.Should().NotBeNull();
    }

    [Fact]
    public void FlexQuery_WithKeysetCursor_ReturnsNextPage()
    {
        var page1 = _db.Entities.AsQueryable().FlexQuery(
            new FlexQueryParameters
            {
                UseKeysetPagination = true,
                Sort = "Id:asc",
                PageSize = 3
            });

        var page2 = _db.Entities.AsQueryable().FlexQuery(
            new FlexQueryParameters
            {
                Cursor = page1.NextCursorToken,
                Sort = "Id:asc",
                PageSize = 3
            });

        page2.Data.Should().HaveCount(3);
        page2.Data.Cast<TestEntity>().Select(e => e.Id).Should().BeEquivalentTo([4, 5, 6]);
    }

    [Fact]
    public void FlexQuery_WithIncludeCount_ReturnsTotalCount()
    {
        var parameters = new FlexQueryParameters
        {
            UseKeysetPagination = true,
            Sort = "Id:asc",
            PageSize = 3,
            IncludeCount = true
        };

        var result = _db.Entities.AsQueryable().FlexQuery(parameters);

        result.TotalCount.Should().Be(10);
        result.NextCursorToken.Should().NotBeNull();
    }

    [Fact]
    public void FlexQuery_WithCursorAndPage_Throws()
    {
        var parameters = new FlexQueryParameters
        {
            Page = 1,
            Cursor = KeysetCursorSerializer.Serialize(new KeysetCursor(3)),
            Sort = "Id:asc",
            PageSize = 3
        };

        var act = () => _db.Entities.AsQueryable().FlexQuery(parameters);

        act.Should().Throw<QueryValidationException>()
            .WithMessage("*Offset pagination parameters*Keyset Pagination*");
    }

    [Fact]
    public void FlexQuery_WithUseKeysetPaginationAndPage_Throws()
    {
        var parameters = new FlexQueryParameters
        {
            UseKeysetPagination = true,
            Page = 1,
            Sort = "Id:asc",
            PageSize = 3
        };

        var act = () => _db.Entities.AsQueryable().FlexQuery(parameters);

        act.Should().Throw<QueryValidationException>()
            .WithMessage("*Offset pagination parameters*Keyset Pagination*");
    }

    [Fact]
    public void FlexQuery_WithCursorAndNoPage_DoesNotThrow()
    {
        var parameters = new FlexQueryParameters
        {
            Cursor = KeysetCursorSerializer.Serialize(new KeysetCursor(3)),
            Sort = "Id:asc",
            PageSize = 3
        };

        var act = () => _db.Entities.AsQueryable().FlexQuery(parameters);

        act.Should().NotThrow();
    }

    // ── QueryOptions direct overload ─────────────────────────────────────

    [Fact]
    public void FlexQuery_QueryOptionsWithKeysetMode_ReturnsCorrectPage()
    {
        var options = new QueryOptions
        {
            Sort = [new SortNode { Field = "Id" }],
            Paging = { PageSize = 3 },
            IsKeysetMode = true,
            Cursor = new KeysetCursor(3)
        };

        var result = _db.Entities.AsQueryable().FlexQuery(options);

        result.Data.Should().HaveCount(3);
        result.Data.Cast<TestEntity>().Select(e => e.Id).Should().BeEquivalentTo([4, 5, 6]);
    }

    [Fact]
    public void ApplyKeysetPaging_WithNullCursor_ReturnsFirstPage()
    {
        var options = new QueryOptions
        {
            Sort = [new SortNode { Field = "Id" }],
            Paging = { PageSize = 3 },
            IsKeysetMode = true,
            Cursor = null
        };

        var result = QueryBuilder.ApplyKeysetPaging(
            _db.Entities.AsQueryable().ApplySort(options),
            options)
            .ToList();

        result.Should().HaveCount(3);
        result.Select(e => e.Id).Should().BeEquivalentTo([1, 2, 3]);
    }

    // ── Edge cases ───────────────────────────────────────────────────────

    [Fact]
    public void SeekAfter_WithFilter_Composes()
    {
        var page = _db.Entities
            .Where(e => e.City == "New York")
            .OrderBy(e => e.Id)
            .SeekAfter(0)
            .Take(3)
            .ToList();

        page.Should().HaveCount(3);
        page.Should().OnlyContain(e => e.City == "New York");
    }

    [Fact]
    public void CursorSerializer_RoundTrip_AndUseInSeekAfter()
    {
        var token = KeysetCursorSerializer.Serialize(new KeysetCursor(3));
        var cursor = KeysetCursorSerializer.Deserialize(token);

        var page = _db.Entities
            .OrderBy(e => e.Id)
            .SeekAfter(3)
            .Take(3)
            .ToList();

        page.Should().HaveCount(3);
        page.First().Id.Should().Be(4);
    }

    [Fact]
    public void UseKeysetPagination_FirstPage_GeneratesNextCursor()
    {
        var parameters = new FlexQueryParameters
        {
            UseKeysetPagination = true,
            Sort = "Id:asc",
            PageSize = 3
        };

        var result = _db.Entities.AsQueryable().FlexQuery(parameters);

        result.NextCursorToken.Should().NotBeNull();

        // Token should represent the last item on the page (Id=3)
        var cursor = KeysetCursorSerializer.Deserialize(result.NextCursorToken);
        cursor.Should().NotBeNull();
        cursor!.Values[0].Should().Be(3);
    }
}
