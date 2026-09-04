using System.Text.Json.Nodes;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Mapping;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared;
using FlexQuery.NET.Tests.Shared.Fixtures;
using Xunit;

namespace FlexQuery.NET.Tests.Mapping;

/// <summary>
/// Mixed entity/DTO navigation properties in a response DTO: the destination property
/// determines the destination type. Entity-typed navigations (reference and collection)
/// map directly without nested DTO maps; DTO-typed navigations require a registered
/// nested entity → DTO element map. A registered nested map never forces conversion of
/// a destination property that declared the entity type.
/// </summary>
[Collection("GlobalMapping")]
public class MixedNavigationEfCoreTests : IDisposable
{
    private readonly SharedTestDbContext _db = SharedTestDbContext.CreateInMemorySeeded();

    public void Dispose() => FlexQueryMapping.Reset();

    public class OrderDto
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    // Test 1 — mixed: entity reference navs + DTO collection nav ----------------------

    public class MixedResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // Entity reference navigations — no nested maps required.
        public Profile? Profile { get; set; }

        // DTO collection navigation — requires Order → OrderDto.
        public List<OrderDto> Orders { get; set; } = [];
    }

    [Fact]
    public async Task Mixed_EntityReferenceAndDtoCollection_Valid()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, MixedResponse>(
            new FlexQueryParameters
            {
                Filter = "Id:eq:2",
                Include = "Profile,Orders",
                Select = "Name,Profile(Bio),Orders(Id,Status)"
            },
            opt =>
            {
                opt.CreateMap<Customer, MixedResponse>();
                opt.CreateMap<Order, OrderDto>();
            });

        var item = JsonNode.Parse(FlexQueryTestJson.Serialize(result))!["data"]![0]!.AsObject();

        // DTO collection navigation materialized as DTO type.
        var orders = item["orders"]!.AsArray();
        orders.Should().NotBeEmpty();
        foreach (var order in orders)
        {
            var o = order!.AsObject();
            o.ContainsKey("id").Should().BeTrue();
            o.ContainsKey("status").Should().BeTrue();
            o.ContainsKey("customer").Should().BeFalse();
            o.ContainsKey("orderItems").Should().BeFalse();
        }

        // Entity reference navigation projected as the entity shape (scalar cut).
        item["profile"].Should().NotBeNull();
        var profile = item["profile"]!.AsObject();
        profile.ContainsKey("bio").Should().BeTrue();
        profile.ContainsKey("customer").Should().BeFalse();

        item["profile"].Should().NotBeNull();
    }

    // Test 2 — entity reference navigation without nested mapping ----------------------

    public class EntityReferenceResponse
    {
        public int Id { get; set; }
        public Profile? Profile { get; set; }
    }

    [Fact]
    public async Task EntityReferenceNavigation_NoNestedMapRequired()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, EntityReferenceResponse>(
            new FlexQueryParameters
            {
                Filter = "Id:eq:2",
                Include = "Profile",
                Select = "Profile(Bio)"
            },
            opt => { });

        result.Data.Should().ContainSingle();
        result.Data[0].Profile!.Bio.Should().NotBeNullOrEmpty();
        // Entity shape: no DTO conversion happened.
    }

    // Test 3 — entity collection navigation without nested mapping ----------------------

    public class EntityCollectionResponse
    {
        public int Id { get; set; }
        public List<Order> Orders { get; set; } = [];
    }

    [Fact]
    public async Task EntityCollectionNavigation_NoNestedMapRequired()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, EntityCollectionResponse>(
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

    // Test 5 — registered nested map must not force conversion of entity-typed property --

    public class EntityCollectionResponse_WithMap
    {
        public int Id { get; set; }
        public List<Order> Orders { get; set; } = [];
    }

    [Fact]
    public async Task RegisteredNestedMap_DoesNotForceConversion_EntityTypePreserved()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, EntityCollectionResponse_WithMap>(
            new FlexQueryParameters
            {
                Filter = "Id:eq:1",
                Include = "Orders",
                Select = "Orders(Id,Status)"
            },
            opt => opt.CreateMap<Order, OrderDto>());

        result.Data.Should().ContainSingle();
        result.Data[0].Orders.Should().NotBeEmpty();
        // Destination property declared List<Order> — the surface remains Order.
        result.Data[0].Orders[0].Status.Should().NotBeNullOrEmpty();
    }

    // Test 6 — DTO-typed navigation without nested map fails deterministically ---------

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
            await _db.Customers.FlexQueryAsync<Customer, DtoCollectionResponse>(
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

