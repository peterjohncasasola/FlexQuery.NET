using System.Text.Json.Nodes;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared;
using FlexQuery.NET.Tests.Shared.Fixtures;
using Xunit;

namespace FlexQuery.NET.Tests.Mapping;

/// <summary>
/// AutoMapper-style mapping API contract tests (EF Core provider, scalar model):
/// CreateMap/ForMember type-level mappings drive the DTO-aware query surface —
/// select, filter, sort, group, aggregate, default projection all resolve through
/// the same mapping; entity-only names stay rejected.
/// </summary>
[Collection("GlobalMapping")]public class AutoMapperStyleMappingEfCoreTests : IDisposable
{
    private readonly ContestantDbContext _db = ContestantDbContext.Create();

    public void Dispose() => _db.Dispose();

    public class ContestantResponse
    {
        public int Id { get; set; }
        public string ContestantName { get; set; } = null!;
        public int Age { get; set; }
        public decimal Score { get; set; }
    }

    private static Action<FlexQuery.NET.EntityFrameworkCore.Options.EfCoreQueryOptions> WithMap()
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

    // Fluent API --------------------------------------------------------------

    [Fact]
    public async Task CreateMap_WithRenamedMember_PropertyTypeInferred()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            P(select: "Id,ContestantName,Age", filter: "Id:eq:1"), WithMap());

        result.Data.Should().ContainSingle();
        result.Data[0].ContestantName.Should().Be("John");

        var item = FirstItem(result);
        item.ContainsKey("contestantName").Should().BeTrue();
        item.ContainsKey("name").Should().BeFalse();
    }

    [Fact]
    public async Task ChainedForMember_BothRenamesResolve()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            P(select: "ContestantName", filter: "ContestantName:eq:Jane"), WithMap());

        result.Data.Should().ContainSingle();
        result.Data[0].ContestantName.Should().Be("Jane");
    }

    [Fact]
    public async Task DuplicateCreateMap_SameConfiguration()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            P(filter: "ContestantName:eq:Bob"), WithMap());

        result.Data.Should().ContainSingle();
        result.Data[0].ContestantName.Should().Be("Bob");
    }

    // Query operations through the same mapping ---------------------------------

    [Fact]
    public async Task Filter_RenamedField_Works()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            P(filter: "ContestantName:contains:oh"), WithMap());

        result.Data.Should().ContainSingle();
        result.Data[0].ContestantName.Should().Be("John");
    }

    [Fact]
    public async Task Sort_RenamedField_Ascending()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            P(sort: "ContestantName:asc"), WithMap());

        result.Data.Select(d => d.ContestantName).Should().Equal("Bob", "Jane", "John");
    }

    [Fact]
    public async Task Sort_RenamedField_Descending()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            P(sort: "ContestantName:desc"), WithMap());

        result.Data.Select(d => d.ContestantName).Should().Equal("John", "Jane", "Bob");
    }

    [Fact]
    public async Task Group_OnRenamedField_PreservesPublicIdentity()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            P(group: "ContestantName"), WithMap());

        result.Data.Should().HaveCount(3);
    }

    [Fact]
    public async Task Aggregate_OnSameNameField_Works()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            P(aggregate: "sum:Score:scoreSum"), WithMap());

        result.Aggregates.Should().NotBeNull();
        Convert.ToDecimal(result.Aggregates!["Score"]["sum"]).Should().Be(255.75m);
    }

    [Fact]
    public async Task DefaultProjection_ExposesOnlyDtoScalars()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            new FlexQueryParameters(), WithMap());

        var item = FirstItem(result);
        item.ContainsKey("contestantName").Should().BeTrue();
        item.ContainsKey("name").Should().BeFalse();
        item.ContainsKey("internalNotes").Should().BeFalse();
    }

    // Surface protection ----------------------------------------------------------

    [Theory]
    [InlineData("Name")]
    [InlineData("InternalNotes")]
    public async Task EntityOnlyNames_Select_Rejected(string field)
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
                P(select: field), WithMap()));

        ex.Message.ToLowerInvariant().Should().Contain(field.ToLowerInvariant());
    }

    [Fact]
    public async Task EntityOnlyName_Filter_Rejected()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
                P(filter: "InternalNotes:eq:x"), WithMap()));

        ex.Message.ToLowerInvariant().Should().Contain("internalnotes");
    }

    // Same-name convention via explicit CreateMap -----------------------------------

    [Fact]
    public async Task SameNameConvention_ExplicitCreateMap_Works()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            P(filter: "Age:gte:25", select: "Id,Age", sort: "Age:desc"), opt =>
                opt.CreateMap<Contestant, ContestantResponse>());

        result.Data.Should().HaveCount(2);
        result.Data[0].Age.Should().Be(31);
    }

    // Computed source expression ------------------------------------------------------

    [Fact]
    public async Task ForMember_ComputedSourceExpression_ProjectionAndFilterWork()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            P(select: "Id,ContestantName", filter: "ContestantName:eq:John"), opt =>
                opt.CreateMap<Contestant, ContestantResponse>()
                    .ForMember(x => x.ContestantName, e => e.Name));

        result.Data.Should().ContainSingle();
        result.Data[0].ContestantName.Should().Be("John");
    }

    // Keyset pagination through a mapped sort field --------------------------------------

    [Fact]
    public async Task KeysetPagination_MappedSortField_Works()
    {
        var firstPage = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            new FlexQueryParameters { Sort = "ContestantName:asc", PageSize = 1, UseKeysetPagination = true }, WithMap());

        firstPage.Data[0].ContestantName.Should().Be("Bob");
        firstPage.NextCursorToken.Should().NotBeNullOrEmpty();

        var secondPage = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            new FlexQueryParameters
            {
                Sort = "ContestantName:asc",
                PageSize = 2,
                UseKeysetPagination = true,
                Cursor = firstPage.NextCursorToken
            }, WithMap());

        secondPage.Data.Select(d => d.ContestantName).Should().Equal("Jane", "John");
    }
}

