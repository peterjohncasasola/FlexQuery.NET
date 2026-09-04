using System.Data;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared.Fixtures;
using FlexQuery.NET.Tests.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace FlexQuery.NET.Tests.Dapper.Integration;

/// <summary>
/// DTO-aware query surface contract tests (Dapper provider) — provider parity
/// with <see cref="DtoQuerySurfaceEfCoreTests"/>. The observable behavior must be
/// equivalent: every operation resolves DTO/public field names through the
/// QuerySurface, and entity-only names are rejected.
/// </summary>
public class DtoQuerySurfaceDapperTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public DtoQuerySurfaceDapperTests()
    {
        var ctx = ContestantDbContext.Create();
        _connection = (SqliteConnection)ctx.Database.GetDbConnection();
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    // Response DTOs under contract (mirrors the EF Core tests) ------------------

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

    private static Action<DapperQueryOptions> WithMapping()
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
        var result = await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
            P(select: "Id,ContestantName,Age"), WithMapping());

        var item = SerializeFirstItem(result);
        item.ContainsKey("id").Should().BeTrue();
        item.ContainsKey("contestantName").Should().BeTrue();
        item.ContainsKey("age").Should().BeTrue();
        item.ContainsKey("name").Should().BeFalse();
        item.ContainsKey("internalNotes").Should().BeFalse();
        item["contestantName"]!.GetValue<string>().Should().Be("John");
    }

    // 2. Filter ---------------------------------------------------------------

    [Fact]
    public async Task Filter_MappedField_FiltersOnEntityName()
    {
        var result = await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
            P(filter: "ContestantName:eq:Jane"), WithMapping());

        result.Data.Should().ContainSingle();
        result.Data[0].ContestantName.Should().Be("Jane");
    }

    [Fact]
    public async Task Filter_MappedField_ContainsOperator_Works()
    {
        var result = await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
            P(filter: "ContestantName:contains:oh"), WithMapping());

        result.Data.Should().ContainSingle();
        result.Data[0].ContestantName.Should().Be("John");
    }

    // 3. Sort -----------------------------------------------------------------

    [Fact]
    public async Task Sort_MappedField_Ascending()
    {
        var result = await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
            P(sort: "ContestantName:asc"), WithMapping());

        result.Data.Select(d => d.ContestantName).Should().Equal("Bob", "Jane", "John");
    }

    [Fact]
    public async Task Sort_MappedField_Descending()
    {
        var result = await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
            P(sort: "ContestantName:desc"), WithMapping());

        result.Data.Select(d => d.ContestantName).Should().Equal("John", "Jane", "Bob");
    }

    // 4. Group ----------------------------------------------------------------

    [Fact]
    public async Task Group_MappedField_GroupsOnEntityName_PreservesPublicIdentity()
    {
        var result = await _connection.FlexQueryAsync<Contestant, ContestantGroupDto>(
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
        var result = await _connection.FlexQueryAsync<Contestant, ContestantStatsDto>(
            P(aggregate: "avg:Age:avgAge"), opt => { });

        result.Aggregates.Should().NotBeNull();
        Convert.ToDecimal(result.Aggregates!["Age"]["avg"]).Should().Be((decimal)Math.Round((25 + 31 + 19) / 3.0));
    }

    [Fact]
    public async Task Aggregate_Sum_OnSameNameField_Works()
    {
        var result = await _connection.FlexQueryAsync<Contestant, ContestantStatsDto>(
            P(aggregate: "sum:Score:totalScore"), opt => { });

        result.Aggregates.Should().NotBeNull();
        Convert.ToDecimal(result.Aggregates!["Score"]["sum"]).Should().Be(255.75m);
    }

    [Fact]
    public async Task Aggregate_Count_OnSameNameField_Works()
    {
        var result = await _connection.FlexQueryAsync<Contestant, ContestantStatsDto>(
            P(aggregate: "count:Age:total"), opt => { });

        result.Aggregates.Should().NotBeNull();
        Convert.ToDecimal(result.Aggregates!["Age"]["count"]).Should().Be(3);
    }

    [Fact]
    public async Task Aggregate_GroupByMappedField_UsesPublicIdentity()
    {
        var result = await _connection.FlexQueryAsync<Contestant, ContestantGroupStatsDto>(
            P(group: "ContestantName", aggregate: "count:ContestantName:c"), opt =>
                opt.MapField<ContestantGroupStatsDto, Contestant, string>(x => x.ContestantName, e => e.Name));

        result.Data.Should().HaveCount(3);
        result.Data.All(d => d.C == 1).Should().BeTrue();

        var item = SerializeFirstItem(result);
        item.ContainsKey("contestantName").Should().BeTrue();
        item.ContainsKey("name").Should().BeFalse();
    }

    // Public surface protection -------------------------------------------------

    [Fact]
    public async Task Select_EntityOnlyName_Throws()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
                P(select: "Name"), WithMapping()));

        ex.Message.Should().Contain("Name");
    }

    [Fact]
    public async Task Select_InternalNotes_Throws()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
                P(select: "InternalNotes"), WithMapping()));

        ex.Message.Should().Contain("InternalNotes");
    }

    [Fact]
    public async Task Filter_EntityOnlyName_Throws()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
                P(filter: "Name:eq:John"), WithMapping()));

        ex.Message.ToLowerInvariant().Should().Contain("name");
    }

    [Fact]
    public async Task Sort_EntityOnlyName_Throws()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
                P(sort: "Name:asc"), WithMapping()));

        ex.Message.Should().Contain("Name");
    }

    [Fact]
    public async Task Group_EntityOnlyName_Throws()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
                P(group: "Name"), WithMapping()));

        ex.Message.Should().Contain("Name");
    }

    [Fact]
    public async Task Aggregate_EntityOnlyName_Throws()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
                P(aggregate: "count:Name:c"), WithMapping()));

        ex.Message.Should().Contain("Name");
    }

    // Same-name convention (no MapField required) -------------------------------

    [Fact]
    public async Task SameNameConvention_FiltersSortsSelectsWithoutMapField()
    {
        var result = await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
            P(filter: "Age:gte:25", select: "Id,Age,Score", sort: "Age:desc"), opt => { });

        result.Data.Should().HaveCount(2);
        result.Data[0].Score.Should().Be(88.0m);
        result.Data[1].Score.Should().Be(95.5m);
    }

    // Default projection ----------------------------------------------------------

    [Fact]
    public async Task DefaultProjection_ExposesOnlyDtoScalars()
    {
        var result = await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
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
            await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
                P(filter: "Age:gte:0"), opt =>
                {
                    opt.MapField<ContestantResponse, Contestant, string>(x => x.ContestantName, e => e.Name);
                    opt.AllowedFields = new HashSet<string> { "Id", "ContestantName" };
                }));

        ex.Message.Should().Contain("Age");
    }

    [Fact]
    public async Task Governance_SortableFields_UsesDtoNames()
    {
        var result = await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
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
            await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
                P(filter: "Score:gt:0"), opt =>
                {
                    opt.MapField<ContestantResponse, Contestant, string>(x => x.ContestantName, e => e.Name);
                    opt.BlockedFields = new HashSet<string> { "Score" };
                }));

        ex.Message.Should().Contain("Score");
    }
}

