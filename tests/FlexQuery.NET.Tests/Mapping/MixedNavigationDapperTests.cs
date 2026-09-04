using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Mapping;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Xunit;

namespace FlexQuery.NET.Tests.Mapping;

/// <summary>
/// Dapper parity for mixed entity/DTO navigation properties: destination property
/// types determine the destination shape — entity-typed navigations map directly,
/// DTO-typed navigations require the registered nested element map.
/// </summary>
[Collection("GlobalMapping")]
public class MixedNavigationDapperTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public MixedNavigationDapperTests()
    {
        var ctx = SharedTestDbContext.CreateSqlite();
        SampleData.Seed(ctx);
        _connection = (SqliteConnection)ctx.Database.GetDbConnection();
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
        FlexQueryMapping.Reset();
    }

    public class OrderDto
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    // Mixed: DTO collection navigation (registered nested map) + entity-typed
    // collection navigation (no map) on the same response type -------------------------

    public class MixedResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // Entity reference navigation — accepted (not hydrated: no include for it).
        public Profile? Profile { get; set; }

        // DTO collection navigation — requires Order → OrderDto.
        public List<OrderDto> Orders { get; set; } = [];
    }

    [Fact]
    public async Task Mixed_EntityReferenceAndDtoCollection_MaterializesDtoTypes()
    {
        var result = await _connection.FlexQueryAsync<Customer, MixedResponse>(
            new FlexQueryParameters
            {
                Filter = "Id:eq:2",
                Include = "Orders",
                Select = "Name,Orders(Id,Status)"
            },
            opt =>
            {
                opt.CreateMap<Customer, MixedResponse>();
                opt.CreateMap<Order, OrderDto>();
            });

        result.Data.Should().ContainSingle();
        var row = result.Data[0];

        // DTO collection navigation materialized as DTO types.
        row.Orders.Should().NotBeEmpty();
        row.Orders.All(o => o is OrderDto).Should().BeTrue();

        // Entity reference navigation: accepted without a nested map (null here —
        // not hydrated, Dapper mapping model does not expose a Profile FK column).
    }

    // Entity collection navigation without nested mapping ------------------------------

    public class EntityCollectionResponse
    {
        public int Id { get; set; }
        public List<Order> Orders { get; set; } = [];
    }

    [Fact]
    public async Task EntityCollectionNavigation_NoNestedMapRequired()
    {
        var result = await _connection.FlexQueryAsync<Customer, EntityCollectionResponse>(
            new FlexQueryParameters
            {
                Filter = "Id:eq:1",
                Include = "Orders",
                Select = "Orders(Id,Status)"
            },
            opt => { });

        result.Data.Should().ContainSingle();
        result.Data[0].Orders.Should().NotBeEmpty();
        result.Data[0].Orders[0].Status.Should().NotBeNullOrEmpty();
    }

    // Registered nested map must not force conversion of entity-typed property ---------

    public class EntityCollectionResponse_WithMap
    {
        public int Id { get; set; }
        public List<Order> Orders { get; set; } = [];
    }

    [Fact]
    public async Task RegisteredNestedMap_DoesNotForceConversion()
    {
        var result = await _connection.FlexQueryAsync<Customer, EntityCollectionResponse_WithMap>(
            new FlexQueryParameters
            {
                Filter = "Id:eq:1",
                Include = "Orders",
                Select = "Orders(Id,Status)"
            },
            opt => opt.CreateMap<Order, OrderDto>());

        result.Data.Should().ContainSingle();
        result.Data[0].Orders.Should().NotBeEmpty();
        // Destination declared List<Order> — stays Order, not OrderDto.
        result.Data[0].Orders[0].Status.Should().NotBeNullOrEmpty();
    }

    // DTO-typed navigation without nested map fails deterministically -------------------

    public class DtoCollectionResponse
    {
        public int Id { get; set; }
        public List<OrderDto> Orders { get; set; } = [];
    }

    [Fact]
    public async Task DtoCollectionNavigation_MissingNestedMap_Rejected()
    {
        FlexQueryMapping.Reset();

        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _connection.FlexQueryAsync<Customer, DtoCollectionResponse>(
                new FlexQueryParameters
                {
                    Filter = "Id:eq:1",
                    Include = "Orders",
                    Select = "Orders(Id,Status)"
                },
                opt => { }));

        ex.Message.Should().Contain("Orders");
    }
}
