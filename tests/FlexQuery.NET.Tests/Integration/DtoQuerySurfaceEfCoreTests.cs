using System.Text.Json.Nodes;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared;
using FlexQuery.NET.Tests.Shared.Fixtures;

namespace FlexQuery.NET.Tests.Integration;

/// <summary>
/// DTO-aware query surface contract tests (EF Core provider).
///
/// The response DTO is the complete public query surface: every operation
/// (select, filter, sort, group, aggregate) must accept DTO field names, resolve
/// renamed fields to their entity expressions internally, and reject entity-only
/// names that the DTO does not expose.
/// </summary>
public class DtoQuerySurfaceEfCoreTests : IDisposable
{
    private readonly ContestantDbContext _db = ContestantDbContext.Create();

    public void Dispose() => _db.Dispose();

    // Response DTOs under contract ---------------------------------------------

    public class ContestantResponse
    {
        public int Id { get; set; }
        public string ContestantName { get; set; } = null!;
        public int Age { get; set; }
        public decimal Score { get; set; }
    }

    public class ContestantGroupDto
    {
        public string ContestantName { get; set; } = string.Empty;
    }

    /// <summary>Exposes the aggregated fields plus the aggregate output aliases.</summary>
    public class ContestantStatsDto
    {
        public int Age { get; set; }
        public decimal Score { get; set; }
        public int AvgAge { get; set; }
        public decimal TotalScore { get; set; }
        public int Total { get; set; }
    }

    public class ContestantGroupStatsDto
    {
        public string ContestantName { get; set; } = string.Empty;
        public int C { get; set; }
    }

    private static Action<FlexQuery.NET.EntityFrameworkCore.Options.EfCoreQueryOptions> WithMapping()
        => opt => opt.MapField<ContestantResponse, Contestant, string>(x => x.ContestantName, e => e.Name);

    private static FlexQueryParameters P(string filter = "", string select = "", string sort = "", string group = "", string aggregate = "")
        => new()
        {
            Filter = string.IsNullOrWhiteSpace(filter) ? null : filter,
            Select = string.IsNullOrWhiteSpace(select) ? null : select,
            Sort = string.IsNullOrWhiteSpace(sort) ? null : sort,
            GroupBy = string.IsNullOrWhiteSpace(group) ? null : group,
            Aggregate = string.IsNullOrWhiteSpace(aggregate) ? null : aggregate
        };

    private static JsonObject SerializeFirstItem<T>(QueryResult<T> result)
        => JsonNode.Parse(FlexQueryTestJson.Serialize(result))!["data"]![0]!.AsObject();

    // 1. Select ---------------------------------------------------------------

    [Fact]
    public async Task Select_MappedField_ReturnsPublicName()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            P(select: "Id,ContestantName,Age"), WithMapping());

        var item = SerializeFirstItem(result);
        item.ContainsKey("id").Should().BeTrue();
        item.ContainsKey("contestantName").Should().BeTrue();
        item.ContainsKey("age").Should().BeTrue();
        item.ContainsKey("name").Should().BeFalse();
        item["contestantName"]!.GetValue<string>().Should().Be("John");
    }

    // 2. Filter ---------------------------------------------------------------

    [Fact]
    public async Task Filter_MappedField_FiltersOnEntityName()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            P(filter: "ContestantName:eq:Jane"), WithMapping());

        result.Data.Should().ContainSingle();
        result.Data[0].ContestantName.Should().Be("Jane");
    }

    [Fact]
    public async Task Filter_MappedField_ContainsOperator_Works()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            P(filter: "ContestantName:contains:oh"), WithMapping());

        result.Data.Should().ContainSingle();
        result.Data[0].ContestantName.Should().Be("John");
    }

    // 3. Sort -----------------------------------------------------------------

    [Fact]
    public async Task Sort_MappedField_Ascending()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            P(sort: "ContestantName:asc"), WithMapping());

        result.Data.Select(d => d.ContestantName).Should().Equal("Bob", "Jane", "John");
    }

    [Fact]
    public async Task Sort_MappedField_Descending()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            P(sort: "ContestantName:desc"), WithMapping());

        result.Data.Select(d => d.ContestantName).Should().Equal("John", "Jane", "Bob");
    }

    // 4. Group ----------------------------------------------------------------

    [Fact]
    public async Task Group_MappedField_GroupsOnEntityName_PreservesPublicIdentity()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantGroupDto>(
            P(group: "ContestantName"), opt =>
                opt.MapField<ContestantGroupDto, Contestant, string>(x => x.ContestantName, e => e.Name));

        result.Data.Should().HaveCount(3);
        result.Data.Select(d => d.ContestantName).Should().BeEquivalentTo("John", "Jane", "Bob");

        var item = SerializeFirstItem(result);
        item.ContainsKey("contestantName").Should().BeTrue();
        item.ContainsKey("name").Should().BeFalse();
    }

    // 5. Aggregate ------------------------------------------------------------

    [Fact]
    public async Task Aggregate_Avg_OnSameNameField_Works()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantStatsDto>(
            P(aggregate: "avg:Age:avgAge"), opt => { });

        result.Aggregates.Should().NotBeNull();
        Convert.ToDecimal(result.Aggregates!["Age"]["avg"]).Should().Be((decimal)Math.Round((25 + 31 + 19) / 3.0));
    }

    [Fact]
    public async Task Aggregate_Sum_OnSameNameField_Works()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantStatsDto>(
            P(aggregate: "sum:Score:totalScore"), opt => { });

        result.Aggregates.Should().NotBeNull();
        Convert.ToDecimal(result.Aggregates!["Score"]["sum"]).Should().Be(255.75m);
    }

    [Fact]
    public async Task Aggregate_Count_OnMappedField_Works()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantStatsDto>(
            P(aggregate: "count:Age:total"), opt => { });

        result.Aggregates.Should().NotBeNull();
        Convert.ToDecimal(result.Aggregates!["Age"]["count"]).Should().Be(3);
    }

    [Fact]
    public async Task Aggregate_GroupByMappedField_UsesPublicIdentity()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantGroupStatsDto>(
            P(group: "ContestantName", aggregate: "count:ContestantName:c"), opt =>
                opt.MapField<ContestantGroupStatsDto, Contestant, string>(x => x.ContestantName, e => e.Name));

        result.Data.Should().HaveCount(3);
        result.Data.All(d => d.C == 1).Should().BeTrue();
    }

    // Public surface protection -------------------------------------------------

    [Fact]
    public async Task Select_EntityOnlyName_Throws()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
                P(select: "Name"), WithMapping()));

        ex.Message.Should().Contain("Name");
    }

    [Fact]
    public async Task Select_InternalNotes_Throws()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
                P(select: "InternalNotes"), WithMapping()));

        ex.Message.Should().Contain("InternalNotes");
    }

    [Fact]
    public async Task Filter_EntityOnlyName_Throws()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
                P(filter: "Name:eq:John"), WithMapping()));

        // The DSL normalizes filter fields; the rejection must reference the field.
        ex.Message.ToLowerInvariant().Should().Contain("name");
    }

    [Fact]
    public async Task Sort_EntityOnlyName_Throws()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
                P(sort: "Name:asc"), WithMapping()));

        ex.Message.Should().Contain("Name");
    }

    [Fact]
    public async Task Group_EntityOnlyName_Throws()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
                P(group: "Name"), WithMapping()));

        ex.Message.Should().Contain("Name");
    }

    [Fact]
    public async Task Aggregate_EntityOnlyName_Throws()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
                P(aggregate: "count:Name:c"), WithMapping()));

        ex.Message.Should().Contain("Name");
    }

    // Same-name convention (no MapField required) -------------------------------

    [Fact]
    public async Task SameNameConvention_FiltersSortsSelectsWithoutMapField()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            P(filter: "Age:gte:25", select: "Id,Age,Score", sort: "Age:desc"), opt => { });

        result.Data.Should().HaveCount(2);
        result.Data[0].Score.Should().Be(88.0m);
        result.Data[1].Score.Should().Be(95.5m);
    }

    // Default projection ----------------------------------------------------------

    [Fact]
    public async Task DefaultProjection_ExposesOnlyDtoScalars()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            new FlexQueryParameters(), WithMapping());

        var item = SerializeFirstItem(result);
        item.ContainsKey("id").Should().BeTrue();
        item.ContainsKey("contestantName").Should().BeTrue();
        item.ContainsKey("age").Should().BeTrue();
        item.ContainsKey("score").Should().BeTrue();
        item.ContainsKey("name").Should().BeFalse();
        item.ContainsKey("internalNotes").Should().BeFalse();
    }

    // Governance -------------------------------------------------------------------

    [Fact]
    public async Task Governance_AllowedFields_UsesDtoNames()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
                P(filter: "Age:gte:0"), opt =>
                {
                    opt.MapField<ContestantResponse, Contestant, string>(x => x.ContestantName, e => e.Name);
                    opt.AllowedFields = new HashSet<string> { "Id", "ContestantName" };
                }));

        // Age is not on the governance whitelist → filter denied.
        ex.Message.Should().Contain("Age");
    }

    [Fact]
    public async Task Governance_AllowedFields_EntityNameRejected()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
                P(select: "ContestantName"), opt =>
                {
                    opt.MapField<ContestantResponse, Contestant, string>(x => x.ContestantName, e => e.Name);
                    opt.AllowedFields = new HashSet<string> { "Id", "Name" };
                }));

        // 'ContestantName' is not on the governance whitelist (which lists entity 'Name').
        ex.Message.Should().Contain("ContestantName");
    }

    [Fact]
    public async Task Governance_SortableFields_UsesDtoNames()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            P(sort: "ContestantName:asc"), opt =>
            {
                opt.MapField<ContestantResponse, Contestant, string>(x => x.ContestantName, e => e.Name);
                opt.SortableFields = new HashSet<string> { "ContestantName" };
            });

        result.Data.Select(d => d.ContestantName).Should().Equal("Bob", "Jane", "John");
    }

    [Fact]
    public async Task Governance_BlockedFields_UsesDtoNames()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
                P(filter: "Score:gt:0"), opt =>
                {
                    opt.MapField<ContestantResponse, Contestant, string>(x => x.ContestantName, e => e.Name);
                    opt.BlockedFields = new HashSet<string> { "Score" };
                }));

        ex.Message.Should().Contain("Score");
    }

    [Fact]
    public async Task Governance_AggregatableFields_UsesDtoNames()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantStatsDto>(
            P(aggregate: "avg:Age:avgAge"), opt =>
            {
                opt.AggregatableFields = new HashSet<string> { "Age" };
            });

        result.Aggregates.Should().NotBeNull();
        Convert.ToDecimal(result.Aggregates!["Age"]["avg"]).Should().Be((decimal)Math.Round((25 + 31 + 19) / 3.0));
    }

    [Fact]
    public async Task Sort_MappedField_SpaceDirection_Ascending()
    {
        // Seed order is John/Jane/Bob — alphabetical is Bob/Jane/John, so a correct
        // mapping-driven sort must reorder the rows (names intentionally out of
        // insertion order).
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            new FlexQueryParameters { Sort = "ContestantName ASC" }, WithMapping());

        result.Data.Select(d => d.ContestantName).Should().Equal("Bob", "Jane", "John");
    }

    [Fact]
    public async Task Sort_MappedField_SpaceDirection_Descending()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            new FlexQueryParameters { Sort = "ContestantName DESC" }, WithMapping());

        result.Data.Select(d => d.ContestantName).Should().Equal("John", "Jane", "Bob");
    }

    [Fact]
    public async Task Sort_MappedField_SpaceDirection_ResolvesThroughMapField()
    {
        // The space-direction sort form binds to FlexQueryBase.Sort and resolves
        // through the DTO surface identically to the colon form.
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            new FlexQueryParameters { Sort = "ContestantName ASC" }, WithMapping());

        result.Data.Select(d => d.ContestantName).Should().Equal("Bob", "Jane", "John");
    }

    [Fact]
    public async Task Sort_EntityOnlyName_SpaceDirection_IsRejected()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
                new FlexQueryParameters { Sort = "Name ASC" }, WithMapping()));

        ex.Message.ToLowerInvariant().Should().Contain("name");
    }

    // Keyset paging through a mapped sort field ------------------------------------

    [Fact]
    public async Task KeysetPaging_MappedSortField_ReturnsCursorAndPages()
    {
        var firstPage = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            new FlexQueryParameters { Sort = "ContestantName:asc", PageSize = 1, UseKeysetPagination = true }, WithMapping());

        firstPage.Data.Should().ContainSingle();
        firstPage.Data[0].ContestantName.Should().Be("Bob");
        firstPage.NextCursorToken.Should().NotBeNullOrEmpty();

        var secondPage = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            new FlexQueryParameters { Sort = "ContestantName:asc", PageSize = 2, UseKeysetPagination = true, Cursor = firstPage.NextCursorToken }, WithMapping());

        secondPage.Data.Select(d => d.ContestantName).Should().Equal("Jane", "John");
    }

    // Include through mapped (renamed) navigations ----------------------------------

    [Fact]
    public async Task Include_UnknownNavigation_IsRejectedInDtoMode()
    {
        // The ContestantResponse DTO exposes no navigation — any include path must be
        // rejected because the public surface has nothing for it to bind to.
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
                new FlexQueryParameters { Include = "SomeNavigation" }, WithMapping()));

        ex.Message.Should().Contain("SomeNavigation");
    }
}

