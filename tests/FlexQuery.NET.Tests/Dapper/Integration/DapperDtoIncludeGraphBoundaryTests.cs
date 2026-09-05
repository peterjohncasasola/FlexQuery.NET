using System.Collections;
using System.Data;
using System.Data.Common;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Mapping;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared.Fixtures;
using FlexQuery.NET.Tests.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FlexQuery.NET.Tests.Dapper.Integration;

/// <summary>
/// Regression tests for the Dapper typed-DTO navigation graph boundary: the response
/// graph is the requested include/expand graph, not the declared DTO TypeMap graph.
///
/// <c>include=Orders</c> materializes only <c>Customer → Orders</c> (with expand
/// windows applied); DTO-declared sibling/deeper navigations (<c>OrderItems</c>,
/// <c>Addresses</c>) stay at their DTO default unless explicitly included, and
/// parents without orders remain in the result.
/// </summary>
[Collection("GlobalMapping")]
public class DapperDtoIncludeGraphBoundaryTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public DapperDtoIncludeGraphBoundaryTests()
    {
        global::FlexQuery.NET.Parsers.Fql.Fql.Register();

        FlexQueryMapping.Reset();
        FlexQueryMapping.Configure(registry =>
        {
            registry.GetOrCreate<Customer, CustomerResponse>();
            registry.GetOrCreate<Order, OrderResponse>();
            registry.GetOrCreate<OrderItem, OrderItemResponse>();
            registry.GetOrCreate<Address, AddressResponse>();
        });

        var ctx = SharedTestDbContext.CreateSqlite();
        SampleData.Seed(ctx);
        _connection = (SqliteConnection)ctx.Database.GetDbConnection();
        if (_connection.State != ConnectionState.Open) _connection.Open();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = OFF;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = """
            INSERT INTO Addresses (Id, CustomerId, Street, City, Province, Country, PostalCode, IsActive) VALUES
                (1, 1, '5th Ave 1', 'New York', NULL, 'US', '10001', 1);
            """;
        cmd.ExecuteNonQuery();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        FlexQueryMapping.Reset();
        _connection.Dispose();
    }

    public class OrderItemResponse
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
    }

    public class OrderResponse
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<OrderItemResponse>? OrderItems { get; set; }
    }

    public class AddressResponse
    {
        public int Id { get; set; }
        public string? City { get; set; }
    }

    public class CustomerResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<OrderResponse> Orders { get; set; } = [];
        public List<AddressResponse>? Addresses { get; set; }
    }

    private static Action<DapperQueryOptions> Fql => opt =>
        opt.QuerySyntax = global::FlexQuery.NET.Parsers.QuerySyntax.Fql;

    private static readonly int[] OrderlessCustomerIds = [4, 5, 6, 7, 8, 9, 10];

    [Fact]
    public async Task Include_WithExpandTake_DoesNotMaterializeUnrequestedNavigations()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Include = "Orders",
                Expand = "Orders(take=1)",
                Page = 1,
                PageSize = 100
            },
            Fql);

        result.Data.Should().HaveCount(10);

        var alice = result.Data.Single(c => c.Id == 1);
        alice.Orders.Should().HaveCount(1);
        alice.Orders.Should().OnlyContain(o => o.OrderItems == null || o.OrderItems.Count == 0);
        alice.Addresses.Should().BeNull();

        foreach (var id in OrderlessCustomerIds)
        {
            var customer = result.Data.Single(c => c.Id == id);
            customer.Orders.Should().BeEmpty($"customer {id} has no orders and must remain");
            customer.Addresses.Should().BeNull();
        }
    }

    [Fact]
    public async Task Include_Alone_DoesNotMaterializeUnrequestedNavigations()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Include = "Orders",
                Page = 1,
                PageSize = 100
            },
            Fql);

        result.Data.Should().HaveCount(10);

        var alice = result.Data.Single(c => c.Id == 1);
        alice.Orders.Should().HaveCount(2);
        alice.Orders.Should().OnlyContain(o => o.OrderItems == null || o.OrderItems.Count == 0);
        alice.Addresses.Should().BeNull();
    }

    [Fact]
    public async Task Include_NestedPath_MaterializesRequestedNestedNavigation()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Include = "Orders,Orders.OrderItems",
                Page = 1,
                PageSize = 100
            },
            Fql);

        var alice = result.Data.Single(c => c.Id == 1);
        var order10001 = alice.Orders.Single(o => o.Id == 10001);
        order10001.OrderItems.Should().HaveCount(2);
        alice.Addresses.Should().BeNull();
    }

    [Fact]
    public async Task Include_SiblingNavigation_MaterializesOnlyRequestedGraph()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Include = "Orders,Addresses",
                Page = 1,
                PageSize = 100
            },
            Fql);

        var alice = result.Data.Single(c => c.Id == 1);
        alice.Orders.Should().HaveCount(2);
        alice.Addresses.Should().NotBeNull();
        alice.Addresses.Should().ContainSingle(a => a.Id == 1);
        alice.Orders.Should().OnlyContain(o => o.OrderItems == null || o.OrderItems.Count == 0);
    }
}
