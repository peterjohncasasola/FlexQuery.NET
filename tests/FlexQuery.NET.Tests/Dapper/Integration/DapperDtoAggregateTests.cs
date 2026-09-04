using System.Data;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared.Fixtures;
using FlexQuery.NET.Tests.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FlexQuery.NET.Tests.Dapper.Integration;

/// <summary>
/// DTO-aware aggregate materialization tests (Dapper provider). Ungrouped aggregates
/// are grand totals flowing into QueryResult.Aggregates; grouped results keep the
/// dynamic group shape when the row DTO does not model aggregate aliases.
/// </summary>
public class DapperDtoAggregateTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public DapperDtoAggregateTests()
    {
        var ctx = ContestantDbContext.Create();
        _connection = (SqliteConnection)ctx.Database.GetDbConnection();
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    // Row DTO — deliberately has NO aggregate alias properties.
    public class ContestantRowDto
    {
        public int Id { get; set; }
        public string ContestantName { get; set; } = string.Empty;
        public int Age { get; set; }
        public decimal Score { get; set; }
    }

    public class ContestantMappedDto
    {
        public decimal TotalScore { get; set; }
    }

    public static TheoryData<string, string, double> AllFunctions => new()
    {
        { "sum:Score:scoreSum", "sum", 255.75 },
        { "avg:Age:ageAverage", "avg", 25 },
        { "min:Score:scoreMin", "min", 72.25 },
        { "max:Score:scoreMax", "max", 95.5 },
        { "count:Id:idCount", "count", 3 }
    };

    [Theory]
    [MemberData(nameof(AllFunctions))]
    public async Task UngroupedAggregate_GoesToAggregatesMetadata(string aggregate, string function, double expected)
    {
        var result = await _connection.FlexQueryAsync<Contestant, ContestantRowDto>(
            new FlexQueryParameters { Aggregate = aggregate, PageSize = 10 });

        result.Aggregates.Should().NotBeNull();
        var fieldKey = aggregate.Split(':')[1];
        Convert.ToDecimal(result.Aggregates![fieldKey][function])
            .Should().Be((decimal)expected);
        result.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SelectPlusAggregate_RowsAndMetadataSeparated()
    {
        var result = await _connection.FlexQueryAsync<Contestant, ContestantRowDto>(
            new FlexQueryParameters
            {
                Select = "ContestantName",
                Aggregate = "sum:Score:scoreSum",
                PageSize = 10
            },
            opt => opt.MapField<ContestantRowDto, Contestant, string>(d => d.ContestantName, e => e.Name));

        result.Aggregates.Should().NotBeNull();
        Convert.ToDecimal(result.Aggregates!["Score"]["sum"]).Should().Be(255.75m);
        result.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task MappedAggregateField_ResolvesThroughQuerySurface()
    {
        // TotalScore → entity Score via MapField; no generated TotalScoreSum on the DTO.
        var result = await _connection.FlexQueryAsync<Contestant, ContestantMappedDto>(
            new FlexQueryParameters { Aggregate = "sum:TotalScore:totalScoreSum" },
            opt => opt.MapField<ContestantMappedDto, Contestant, decimal>(d => d.TotalScore, e => e.Score));

        result.Aggregates.Should().NotBeNull();
        Convert.ToDecimal(result.Aggregates!["TotalScore"]["sum"]).Should().Be(255.75m);
    }

    [Fact]
    public async Task GroupedAggregate_DynamicShape_WhenDtoDoesNotModelAliases()
    {
        var result = await _connection.FlexQueryAsync<Contestant, ContestantRowDto>(
            new FlexQueryParameters
            {
                GroupBy = "Age",
                Aggregate = "count:Id:contestantCount"
            });

        result.Data.Should().HaveCount(3);
        var serialized = FlexQueryTestJson.Serialize(result);
        serialized.ToLowerInvariant().Should().Contain("contestantcount");
    }
}
