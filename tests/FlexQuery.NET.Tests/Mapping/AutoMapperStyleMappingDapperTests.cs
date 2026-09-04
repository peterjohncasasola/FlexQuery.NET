using System.Data;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;
using FlexQuery.NET.Tests.Shared;
using Xunit;

namespace FlexQuery.NET.Tests.Mapping;

/// <summary>
/// AutoMapper-style mapping API parity (Dapper provider): scalar operations resolve
/// through the same mapping; nested navigation materializes through the registered
/// TypeMap graph with no raw entity leakage.
/// </summary>
[Collection("GlobalMapping")]public class AutoMapperStyleMappingDapperTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public AutoMapperStyleMappingDapperTests()
    {
        var ctx = ContestantDbContext.Create();
        _connection = (SqliteConnection)ctx.Database.GetDbConnection();
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    public class ContestantResponse
    {
        public int Id { get; set; }
        public string ContestantName { get; set; } = null!;
        public int Age { get; set; }
        public decimal Score { get; set; }
    }

    private static Action<DapperQueryOptions> WithMap()
        => opt => opt.CreateMap<Contestant, ContestantResponse>()
            .ForMember(x => x.ContestantName, e => e.Name);

    private static FlexQueryParameters P(string filter = "", string select = "", string sort = "", string group = "", string aggregate = "")
        => new()
        {
            Filter = string.IsNullOrWhiteSpace(filter) ? null : filter,
            Select = string.IsNullOrWhiteSpace(select) ? null : select,
            Sort = string.IsNullOrWhiteSpace(sort) ? null : sort,
            GroupBy = string.IsNullOrWhiteSpace(group) ? null : group,
            Aggregate = string.IsNullOrWhiteSpace(aggregate) ? null : aggregate
        };

    private static JsonObject FirstItem<T>(QueryResult<T> result)
        => JsonNode.Parse(FlexQueryTestJson.Serialize(result))!["data"]![0]!.AsObject();

    [Fact]
    public async Task Select_RenamedField_ReturnsPublicName()
    {
        var result = await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
            P(select: "Id,ContestantName,Age", filter: "Id:eq:1"), WithMap());

        result.Data.Should().ContainSingle();
        result.Data[0].ContestantName.Should().Be("John");

        var item = FirstItem(result);
        item.ContainsKey("contestantName").Should().BeTrue();
        item.ContainsKey("name").Should().BeFalse();
    }

    [Fact]
    public async Task Filter_RenamedField_Works()
    {
        var result = await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
            P(filter: "ContestantName:eq:Jane"), WithMap());

        result.Data.Should().ContainSingle();
        result.Data[0].ContestantName.Should().Be("Jane");
    }

    [Fact]
    public async Task Sort_RenamedField_Descending()
    {
        var result = await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
            P(sort: "ContestantName:desc"), WithMap());

        result.Data.Select(d => d.ContestantName).Should().Equal("John", "Jane", "Bob");
    }

    [Fact]
    public async Task Group_OnRenamedField_Works()
    {
        var result = await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
            P(group: "ContestantName"), WithMap());

        result.Data.Should().HaveCount(3);
    }

    [Fact]
    public async Task Aggregate_OnSameNameField_Works()
    {
        var result = await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
            P(aggregate: "sum:Score:scoreSum"), WithMap());

        result.Aggregates.Should().NotBeNull();
        Convert.ToDecimal(result.Aggregates!["Score"]["sum"]).Should().Be(255.75m);
    }

    [Fact]
    public async Task EntityOnlyName_Rejected()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQuery.NET.Exceptions.FlexQueryException>(async () =>
            await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
                P(select: "Name"), WithMap()));

        ex.Message.ToLowerInvariant().Should().Contain("name");
    }

    [Fact]
    public async Task DefaultProjection_ExposesOnlyDtoScalars()
    {
        var result = await _connection.FlexQueryAsync<Contestant, ContestantResponse>(
            new FlexQueryParameters(), WithMap());

        var item = FirstItem(result);
        item.ContainsKey("contestantName").Should().BeTrue();
        item.ContainsKey("name").Should().BeFalse();
        item.ContainsKey("internalNotes").Should().BeFalse();
    }
}

