using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Mapping;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared;
using FlexQuery.NET.Tests.Shared.Fixtures;
using System.Text.Json.Nodes;
using Xunit;

namespace FlexQuery.NET.Tests.Mapping;

/// <summary>
/// Regression test for the EF Core "The LINQ expression 'e' could not be translated"
/// failure on include-only (no select) typed DTO queries whose nested navigation is
/// projected through a registered Order → OrderResponse type map.
/// </summary>
[Collection("GlobalMapping")]
public class IncludeOnlyTypeMapProjectionTests : IDisposable
{
    private readonly SharedTestDbContext _db = SharedTestDbContext.CreateInMemorySeeded();

    public void Dispose() => FlexQueryMapping.Reset();

    // Mirrors the user's model: entity-typed reference navigations on the response DTO
    // plus a DTO-typed collection navigation with a registered nested type map.
    public class OrderSlim
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class CustomerSlimResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // Entity-typed reference navigation (same-name convention — no map needed).
        public Profile? Profile { get; set; }

        // DTO-typed collection navigation (nested Order → OrderSlim map required).
        public List<OrderSlim> Orders { get; set; } = [];
    }

    [Fact]
    public async Task IncludeOnly_DefaultProjection_ThroughNestedTypeMap_ServerSide()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerSlimResponse>(
            new FlexQueryParameters
            {
                Filter = "Id:eq:1",
                Include = "Profile,Orders",
                PageSize = 3
            },
            opt =>
            {
                opt.CreateMap<Customer, CustomerSlimResponse>()
                    .ForMember(dto => dto.Name, entity => entity.Name);
                opt.CreateMap<Order, OrderSlim>();
            });

        result.Data.Should().NotBeEmpty();
        var item = JsonNode.Parse(FlexQueryTestJson.Serialize(result))!["data"]![0]!.AsObject();
        item.ContainsKey("name").Should().BeTrue();
        item["profile"].Should().NotBeNull();

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
    }

    [Fact]
    public async Task IncludeOnly_NoSelect_WithNestedSelect_TrackedFallback_AlsoWorks()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerSlimResponse>(
            new FlexQueryParameters
            {
                Filter = "Id:eq:1",
                Include = "Orders",
                Select = "Name,Orders(Id,Status)"
            },
            opt =>
            {
                opt.UseNoTracking = false; // client-side mapping fallback path
                opt.CreateMap<Customer, CustomerSlimResponse>()
                    .ForMember(dto => dto.Name, entity => entity.Name);
                opt.CreateMap<Order, OrderSlim>();
            });

        result.Data.Should().ContainSingle();
        var item = JsonNode.Parse(FlexQueryTestJson.Serialize(result))!["data"]![0]!.AsObject();
        item["orders"]!.AsArray().Should().NotBeEmpty();
        item["orders"]![0]!.AsObject().ContainsKey("status").Should().BeTrue();
    }
}
