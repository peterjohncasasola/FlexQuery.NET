using System.Text.Json.Nodes;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared;
using FlexQuery.NET.Tests.Shared.Fixtures;
using Xunit;

namespace FlexQuery.NET.Tests.Mapping;

/// <summary>
/// Nested DTO graph mapping tests (EF Core): same-name navigation convention via
/// nested registered TypeMaps, recursive DTO projection (no raw entity leakage),
/// renamed navigation via ForNavigation, and include/expand against DTO surface.
/// </summary>
[Collection("GlobalMapping")]public class NestedTypeMapEfCoreTests : IDisposable
{
    private readonly SharedTestDbContext _db = SharedTestDbContext.CreateInMemorySeeded();

    public void Dispose() => _db.Dispose();

    public class OrderSlimDto
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<OrderItemSlimDto> OrderItems { get; set; } = new();
    }

    public class OrderItemSlimDto
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
    }

    public class CustomerResponse
    {
        public int Id { get; set; }
        public string CustomerFullName { get; set; } = string.Empty;
        public List<OrderSlimDto> Orders { get; set; } = new();
    }

    public class RecentOrdersResponse
    {
        public int Id { get; set; }
        public List<OrderSlimDto> RecentOrders { get; set; } = new();
    }

    private static JsonObject SerializeData<T>(QueryResult<T> result)
        => JsonNode.Parse(FlexQueryTestJson.Serialize(result))!["data"]![0]!.AsObject();

    // Same-name navigation convention with nested registered type maps ----------------

    [Fact]
    public async Task SameNameNavigation_WithNestedTypeMaps_ProjectsDtoTypesRecursively()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Select = "CustomerFullName,Orders(Id,Status,OrderItems(Id,Quantity))",
                Include = "Orders,Orders.OrderItems",
                Filter = "Id:eq:1"
            },
            opt =>
            {
                opt.CreateMap<Customer, CustomerResponse>()
                    .ForMember(x => x.CustomerFullName, e => e.Name);
                opt.CreateMap<Order, OrderSlimDto>();
                opt.CreateMap<OrderItem, OrderItemSlimDto>();
            });

        var item = SerializeData(result);
        item.ContainsKey("customerFullName").Should().BeTrue();
        item.ContainsKey("name").Should().BeFalse();

        var orders = item["orders"]!.AsArray();
        orders.Should().NotBeEmpty();

        foreach (var order in orders)
        {
            var orderObj = order!.AsObject();
            orderObj.ContainsKey("id").Should().BeTrue();
            orderObj.ContainsKey("status").Should().BeTrue();
            orderObj.ContainsKey("orderDate").Should().BeFalse();
            orderObj.ContainsKey("total").Should().BeFalse();
            orderObj.ContainsKey("customerId").Should().BeFalse();
            orderObj.ContainsKey("customer").Should().BeFalse();

            foreach (var orderItem in orderObj["orderItems"]!.AsArray())
            {
                var oi = orderItem!.AsObject();
                oi.ContainsKey("id").Should().BeTrue();
                oi.ContainsKey("quantity").Should().BeTrue();
                oi.ContainsKey("sku").Should().BeFalse();
                oi.ContainsKey("price").Should().BeFalse();
            }
        }
    }

    // Renamed navigation via ForNavigation ---------------------------------------------

    [Fact]
    public async Task ForNavigation_RenamedCollection_MapsIncludeExpandAndProjection()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, RecentOrdersResponse>(
            new FlexQueryParameters
            {
                Select = "RecentOrders(Id,Status)",
                Include = "RecentOrders",
                Expand = "RecentOrders(take=2; sort=Id:desc)",
                Filter = "Id:eq:1"
            },
            opt =>
            {
                opt.CreateMap<Customer, RecentOrdersResponse>()
                    .ForNavigation(x => x.RecentOrders, e => e.Orders);
                opt.CreateMap<Order, OrderSlimDto>();
            });

        var item = SerializeData(result);
        item.ContainsKey("recentOrders").Should().BeTrue();
        item.ContainsKey("orders").Should().BeFalse();

        var orders = item["recentOrders"]!.AsArray();
        orders.Count.Should().BeLessThanOrEqualTo(2);
        var ids = orders.Select(o => o!["id"]!.GetValue<int>()).ToList();
        ids.SequenceEqual(ids.OrderByDescending(i => i)).Should().BeTrue();
    }
}

