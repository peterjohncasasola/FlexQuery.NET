using System.Collections;
using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared;
using FlexQuery.NET.Tests.Shared.Fixtures;
using FlexQuery.NET.Tests.Shared.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FlexQuery.NET.Tests.Integration;

/// <summary>
/// Regression tests for the default collection-include join semantics.
///
/// Contract: a collection include is parent-preserving by default — conceptually
/// <c>Customer LEFT JOIN Orders</c>. A parent entity must never be removed from the
/// result merely because its collection contains no rows, and an empty collection
/// must materialize as an empty list (never null, never a dropped parent).
///
/// The same semantics apply to nested paths (<c>include=Orders,Orders.OrderItems</c>)
/// and must not break expand windows (filter/sort/take), DTO projection, or Dapper.
/// These tests use a seed containing customers without orders (David 4, Eve 5,
/// Frank 6, Grace 7, Hank 8, Ivy 9, Jack 10) so any inner-join regression on the
/// default include path fails here.
/// </summary>
public class CollectionIncludeParentPreservingTests
{
    private static (SharedTestDbContext Db, List<string> Sql) CreateCaptureContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var sqlStatements = new List<string>();
        var options = new DbContextOptionsBuilder<SharedTestDbContext>()
            .UseSqlite(connection)
            .LogTo(message =>
            {
                if (message.Contains("Executed DbCommand", StringComparison.Ordinal))
                {
                    sqlStatements.Add(message);
                }
            }, LogLevel.Information)
            .Options;

        var db = new SharedTestDbContext(options);
        db.Database.EnsureCreated();
        SampleData.Seed(db);
        return (db, sqlStatements);
    }

    /// <summary>The customers that have no orders in the shared seed.</summary>
    private static readonly int[] OrderlessCustomerIds = [4, 5, 6, 7, 8, 9, 10];

    private static List<string> DataQueries(List<string> sql)
        => sql
            .Where(s => s.Contains("SELECT", StringComparison.Ordinal)
                        && s.Contains("Customers", StringComparison.Ordinal)
                        && s.Contains("Orders", StringComparison.Ordinal))
            .ToList();

    private static object? Prop(object instance, string name)
        => instance.GetType().GetProperty(name,
               System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
           ?.GetValue(instance);

    private static List<object> Items(object? collection)
        => collection is IEnumerable enumerable && collection is not string
            ? enumerable.Cast<object>().ToList()
            : [];

    // Test 1 — simple collection include ------------------------------------------------

    [Fact]
    public async Task Include_SimpleCollection_ParentsPreserved_AndSqlIsLeftJoin()
    {
        var (db, sql) = CreateCaptureContext();
        try
        {
            var parameters = new FlexQueryParameters
            {
                Include = "Orders",
                PageSize = 100
            };

            var result = await db.Customers.FlexQueryAsync(parameters, opt => { });

            // Every seeded customer remains — including the seven without orders.
            result.Data.Should().HaveCount(10);

            foreach (var id in OrderlessCustomerIds)
            {
                var customer = result.Data.Single(c => (int)Prop(c, "Id")! == id);
                var orders = Items(Prop(customer, "Orders"));
                orders.Should().BeEmpty($"customer {id} has no orders — the include must not drop the parent");
            }

            // Customers with orders keep them (parent + collection rows).
            var alice = result.Data.Single(c => (int)Prop(c, "Id")! == 1);
            Items(Prop(alice, "Orders")).Should().HaveCount(2);

            // The generated SQL must be parent-preserving: LEFT JOIN, never INNER JOIN.
            var dataQueries = DataQueries(sql);
            dataQueries.Should().NotBeEmpty();
            dataQueries.Should().OnlyContain(q => q.Contains("LEFT JOIN", StringComparison.OrdinalIgnoreCase));
            dataQueries.Should().NotContain(q => q.Contains("INNER JOIN", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            db.Dispose();
        }
    }

    // Test 2 — nested collection include --------------------------------------------------

    [Fact]
    public async Task Include_NestedCollection_ParentsAndEmptyIntermediateCollectionsPreserved()
    {
        var (db, sql) = CreateCaptureContext();
        try
        {
            var parameters = new FlexQueryParameters
            {
                Include = "Orders,Orders.OrderItems",
                PageSize = 100
            };

            var result = await db.Customers.FlexQueryAsync(parameters, opt => { });

            // Customer without Orders → Customer remains with Orders = [].
            result.Data.Should().HaveCount(10);
            foreach (var id in OrderlessCustomerIds)
            {
                var customer = result.Data.Single(c => (int)Prop(c, "Id")! == id);
                Items(Prop(customer, "Orders")).Should().BeEmpty();
            }

            // Customer with Order but no OrderItems → Customer remains, Order remains,
            // OrderItems = []. Bob's order 10003 has no items in the seed.
            var bob = result.Data.Single(c => (int)Prop(c, "Id")! == 2);
            var bobOrders = Items(Prop(bob, "Orders"));
            bobOrders.Should().ContainSingle();
            var order10003 = bobOrders.Single(o => (int)Prop(o, "Id")! == 10003);
            Items(Prop(order10003, "OrderItems")).Should().BeEmpty();

            // Populated chains stay populated: Alice's order 10001 has two items.
            var alice = result.Data.Single(c => (int)Prop(c, "Id")! == 1);
            var order10001 = Items(Prop(alice, "Orders")).Single(o => (int)Prop(o, "Id")! == 10001);
            Items(Prop(order10001, "OrderItems")).Should().HaveCount(2);

            var dataQueries = DataQueries(sql);
            dataQueries.Should().OnlyContain(q => q.Contains("LEFT JOIN", StringComparison.OrdinalIgnoreCase));
            dataQueries.Should().NotContain(q => q.Contains("INNER JOIN", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            db.Dispose();
        }
    }

    // Tests C/D — single-level expansion on a relational provider ------------------------
    // Single-level windows (take only, or filter+sort+take) translate without APPLY on
    // SQLite: EF Core renders them as LEFT JOIN over a ROW_NUMBER-windowed derived table
    // (PARTITION BY the correlation key), so the generated SQL and the materialized
    // semantics are both observable here.

    [Fact]
    public async Task Include_ExpandTakeOnly_ParentsPreserved_AndWindowedJoinIsLeftJoin()
    {
        var (db, sql) = CreateCaptureContext();
        try
        {
            var parameters = new FlexQueryParameters
            {
                Include = "Orders",
                Expand = "Orders(take=1)",
                PageSize = 100
            };

            var result = await db.Customers.FlexQueryAsync(parameters, opt => { });

            // All parents remain; each receives at most 1 order.
            result.Data.Should().HaveCount(10);
            foreach (var id in OrderlessCustomerIds)
            {
                var customer = result.Data.Single(c => (int)Prop(c, "Id")! == id);
                Items(Prop(customer, "Orders")).Should().BeEmpty($"customer {id} has no orders — the parent must remain");
            }

            foreach (var id in new[] { 1, 2, 3 })
            {
                var orders = Items(Prop(result.Data.Single(c => (int)Prop(c, "Id")! == id), "Orders"));
                orders.Should().HaveCount(1, $"customer {id} must receive at most 1 order (per-parent take)");
            }

            // Take-only windows need a deterministic order to define which records satisfy
            // the take: EF renders ROW_NUMBER() OVER (PARTITION BY CustomerId ORDER BY ...).
            // That windowing must sit inside a LEFT JOIN — never INNER JOIN.
            var dataQueries = DataQueries(sql);
            dataQueries.Should().NotBeEmpty();
            dataQueries.Should().OnlyContain(q => q.Contains("LEFT JOIN", StringComparison.OrdinalIgnoreCase));
            dataQueries.Should().NotContain(q => q.Contains("INNER JOIN", StringComparison.OrdinalIgnoreCase));
            dataQueries.Should().Contain(q =>
                q.Contains("ROW_NUMBER", StringComparison.OrdinalIgnoreCase)
                && q.Contains("PARTITION BY", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            db.Dispose();
        }
    }

    [Fact]
    public async Task Include_ExpandFilteredSortedTake_ParentsPreserved_AndWindowedJoinIsLeftJoin()
    {
        var (db, sql) = CreateCaptureContext();
        try
        {
            var parameters = new FlexQueryParameters
            {
                Include = "Orders",
                Expand = "Orders(take=1;filter=Status:eq:Delivered;sort=Id:desc)",
                PageSize = 100
            };

            var result = await db.Customers.FlexQueryAsync(parameters, opt => { });

            // All parents remain — including Alice (1) and Carol (3), whose orders are all
            // non-Delivered and therefore filtered out by the expand window entirely.
            result.Data.Should().HaveCount(10);
            foreach (var id in OrderlessCustomerIds)
            {
                var customer = result.Data.Single(c => (int)Prop(c, "Id")! == id);
                Items(Prop(customer, "Orders")).Should().BeEmpty($"customer {id} has no orders — the parent must remain");
            }

            // Alice: only Shipped/Pending orders → all filtered out → collection empty,
            // customer retained.
            Items(Prop(result.Data.Single(c => (int)Prop(c, "Id")! == 1), "Orders"))
                .Should().BeEmpty("Alice has no Delivered orders — the filter must not drop her");

            // Bob: exactly one Delivered order (10003) → the window keeps it.
            var bobOrders = Items(Prop(result.Data.Single(c => (int)Prop(c, "Id")! == 2), "Orders"));
            bobOrders.Should().ContainSingle();
            ((int)Prop(bobOrders[0], "Id")!).Should().Be(10003);

            // Carol: only a Cancelled order → filtered out → empty, retained.
            Items(Prop(result.Data.Single(c => (int)Prop(c, "Id")! == 3), "Orders"))
                .Should().BeEmpty("Carol has no Delivered orders — the filter must not drop her");

            var dataQueries = DataQueries(sql);
            dataQueries.Should().OnlyContain(q => q.Contains("LEFT JOIN", StringComparison.OrdinalIgnoreCase));
            dataQueries.Should().NotContain(q => q.Contains("INNER JOIN", StringComparison.OrdinalIgnoreCase));
            // The filter executes in SQL as a parameter (Delivered = 9 chars), not client-side.
            dataQueries.Should().Contain(q => q.Contains("Size = 9", StringComparison.Ordinal));
        }
        finally
        {
            db.Dispose();
        }
    }

    // Test 3 — nested expansion (filter/sort/take) ---------------------------------------
    // Deep per-parent takes require APPLY (SQL Server renders OUTER APPLY); SQLite
    // cannot translate that shape, so the semantics are verified on InMemory like the
    // existing DeepCollectionExpansionTests suite.

    public class OrderItemDto
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
    }

    public class OrderDto
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<OrderItemDto> OrderItems { get; set; } = [];
    }

    public class CustomerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<OrderDto> Orders { get; set; } = [];
    }

    [Fact]
    public async Task Include_NestedCollection_WithExpandWindows_ParentsPreserved_AndWindowsApplied()
    {
        using var db = SharedTestDbContext.CreateInMemorySeeded();

        // Extra Delivered orders for Bob (2): 10006 (item 10) and 10005 (item 12).
        // With take=1;sort=Id desc, Bob must get exactly order 10006.
        db.Orders.AddRange(
            new Order
            {
                Id = 10006,
                CustomerId = 2,
                Status = "Delivered",
                Total = 10m,
                Number = "SO-EXP-1",
                OrderDate = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                OrderItems = [new OrderItem { Id = 10, OrderId = 10006, Quantity = 1, Price = 5m, Sku = "SKU-E10" }]
            },
            new Order
            {
                Id = 10005,
                CustomerId = 2,
                Status = "Delivered",
                Total = 20m,
                Number = "SO-EXP-2",
                OrderDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                OrderItems = [new OrderItem { Id = 12, OrderId = 10005, Quantity = 2, Price = 10m, Sku = "SKU-E12" }]
            });
        db.SaveChanges();

        var parameters = new FlexQueryParameters
        {
            Include = "Orders,Orders.OrderItems",
            Expand = "Orders(take=1;filter=Status:eq:Delivered;sort=Id:desc),Orders.OrderItems(take=5)",
            PageSize = 100
        };

        var result = await db.Customers.FlexQueryAsync<Customer, CustomerDto>(parameters, opt =>
        {
            opt.CreateMap<Customer, CustomerDto>()
                .ForNavigation(d => d.Orders, e => e.Orders);
            opt.CreateMap<Order, OrderDto>();
            opt.CreateMap<OrderItem, OrderItemDto>();
        });

        // Every customer remains, including those with no orders and those whose orders
        // were all filtered out by the expand window.
        result.Data.Should().HaveCount(10);
        foreach (var id in OrderlessCustomerIds)
        {
            var customer = result.Data.Single(c => c.Id == id);
            customer.Orders.Should().BeEmpty($"customer {id} has no orders — the parent must remain");
        }

        // Alice only has Shipped/Pending orders — the Delivered filter empties her
        // collection, but the customer row itself must survive.
        var alice = result.Data.Single(c => c.Id == 1);
        alice.Orders.Should().BeEmpty();

        // Bob: at most 1 Delivered order, sorted Id DESC → 10006 with both items.
        var bob = result.Data.Single(c => c.Id == 2);
        var bobOrder = bob.Orders.Should().ContainSingle().Subject;
        bobOrder.Id.Should().Be(10006);
        bobOrder.Status.Should().Be("Delivered");
        bobOrder.OrderItems.Select(i => i.Id).Should().Equal([10]);

        // Carol has only a Cancelled order → remains with an empty collection.
        var carol = result.Data.Single(c => c.Id == 3);
        carol.Orders.Should().BeEmpty();
    }

    // Test 4 — DTO EF Core ----------------------------------------------------------------

    public class CustomerOrderDto
    {
        public int Id { get; set; }
        public string CustomerFullName { get; set; } = string.Empty;
        public List<Order>? Orders { get; set; }
    }

    [Fact]
    public async Task Include_DtoEfCore_ParentsPreserved_AndSqlIsLeftJoin()
    {
        var (db, sql) = CreateCaptureContext();
        try
        {
            var parameters = new FlexQueryParameters
            {
                Include = "Orders",
                PageSize = 100
            };

            var result = await db.Customers.FlexQueryAsync<Customer, CustomerOrderDto>(parameters, opt =>
                opt.MapField<CustomerOrderDto, Customer, string>(d => d.CustomerFullName, e => e.Name));

            result.Data.Should().HaveCount(10);
            foreach (var id in OrderlessCustomerIds)
            {
                var customer = result.Data.Single(c => c.Id == id);
                customer.Orders.Should().NotBeNull().And.BeEmpty(
                    $"customer {id} has no orders — the DTO projection must keep the parent with an empty collection");
            }

            var alice = result.Data.Single(c => c.Id == 1);
            alice.Orders.Should().NotBeNull();
            alice.Orders.Should().HaveCount(2);
            alice.Orders.Should().OnlyContain(o => o.Status == "Shipped" || o.Status == "Pending");

            var dataQueries = DataQueries(sql);
            dataQueries.Should().NotBeEmpty();
            dataQueries.Should().OnlyContain(q => q.Contains("LEFT JOIN", StringComparison.OrdinalIgnoreCase));
            dataQueries.Should().NotContain(q => q.Contains("INNER JOIN", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            db.Dispose();
        }
    }

    // Test 5 — Dapper (behavioral parity, not identical SQL) --------------------------------

    [Fact]
    public async Task Include_Dapper_ParentsPreserved_WithEmptyCollections()
    {
        var db = SqlProjectionDbContext.CreateSeeded();
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();
        try
        {
            var parameters = new FlexQueryParameters
            {
                Include = "Orders",
                PageSize = 100
            };

            var result = await ((DbConnection)connection).FlexQueryAsync<Customer>(parameters, opt =>
                opt.UseModel(SharedFlexQueryModel.Instance));
            result.Data.Should().HaveCount(10);
            foreach (var id in OrderlessCustomerIds)
            {
                dynamic customer = result.Data.Single(c => (int)Prop(c, "Id")! == id);
                var orders = ((IEnumerable)Prop(customer, "Orders")!).Cast<object>().ToList();
                orders.Should().BeEmpty($"customer {id} has no orders — the parent must remain with an empty collection");
            }

            dynamic alice = result.Data.Single(c => (int)Prop(c, "Id")! == 1);
            ((IEnumerable)Prop(alice, "Orders")!).Cast<object>().Should().HaveCount(2);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact]
    public async Task Include_Dapper_Http_ParentsPreserved_WithEmptyCollections()
    {
        var db = SqlProjectionDbContext.CreateSeeded();
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();

        using var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.UseStartup<DemoApiStartup>();
                webBuilder.ConfigureTestServices(services => services.AddSingleton(connection as System.Data.IDbConnection));
            })
            .Start();
        using var client = host.GetTestClient();

        try
        {
            var response = await client.GetAsync("/api/users?include=orders&pageSize=100");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var items = json.GetProperty("Data").EnumerateArray().ToList();
            items.Should().HaveCount(10);

            foreach (var id in OrderlessCustomerIds)
            {
                var customer = items.Single(x => x.GetProperty("Id").GetInt32() == id);
                var orders = customer.GetProperty("Orders").EnumerateArray().ToList();
                orders.Should().BeEmpty($"customer {id} has no orders — the parent must remain with an empty collection");
            }

            var alice = items.Single(x => x.GetProperty("Id").GetInt32() == 1);
            alice.GetProperty("Orders").EnumerateArray().Should().HaveCount(2);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }
}
