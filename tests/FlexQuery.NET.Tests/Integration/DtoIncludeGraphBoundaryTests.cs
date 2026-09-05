using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FlexQuery.NET.Tests.Integration;

/// <summary>
/// Regression tests for the typed-DTO navigation graph boundary: the response graph is
/// the requested include/expand graph, not the full declared DTO TypeMap graph.
///
/// <c>include=Orders</c> (with or without <c>expand=Orders(...)</c>) must materialize
/// only <c>Customer → Orders</c>. DTO-declared deeper navigations (e.g.
/// <c>OrderDto.OrderItems</c>, even when nested TypeMaps are registered) must stay at
/// their DTO default unless <c>Orders.OrderItems</c> is explicitly included.
/// </summary>
public class DtoIncludeGraphBoundaryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly List<string> _sql = [];
    private readonly SharedTestDbContext _db;

    public DtoIncludeGraphBoundaryTests()
    {
        var options = new DbContextOptionsBuilder<SharedTestDbContext>()
            .UseSqlite(_connection)
            .LogTo(m => { if (m.Contains("Executed DbCommand", StringComparison.Ordinal)) _sql.Add(m); },
                LogLevel.Information)
            .Options;

        _db = new SharedTestDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        SampleData.Seed(_db);
        _db.Database.ExecuteSqlRaw("""
            INSERT INTO Addresses (Id, Street, City, CustomerId, CustomerId1) VALUES
                (1, '5th Ave 1', 'New York', 1, 1),
                (2, 'Baker St 3', 'London', 2, 2);
            """);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    public class OrderItemDto
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
    }

    public class OrderDto
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<OrderItemDto>? OrderItems { get; set; }
    }

    public class AddressDto
    {
        public int Id { get; set; }
        public string? City { get; set; }
    }

    public class CustomerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<OrderDto> Orders { get; set; } = [];
        public List<AddressDto>? Addresses { get; set; }
    }

    private static Action<FlexQuery.NET.EntityFrameworkCore.Options.EfCoreQueryOptions> ConfigureMaps()
        => opt =>
        {
            opt.CreateMap<Customer, CustomerDto>()
                .ForNavigation(d => d.Orders, e => e.Orders);
            opt.CreateMap<Order, OrderDto>();
            opt.CreateMap<OrderItem, OrderItemDto>();
            opt.CreateMap<Address, AddressDto>();
        };

    private List<string> DataQueries()
        => _sql
            .Where(s => s.Contains("SELECT", StringComparison.Ordinal)
                        && s.Contains("Customers", StringComparison.Ordinal)
                        && s.Contains("Orders", StringComparison.Ordinal))
            .ToList();

    private static readonly int[] OrderlessCustomerIds = [4, 5, 6, 7, 8, 9, 10];

    [Fact]
    public async Task Include_WithExpandTake_DoesNotMaterializeUnrequestedNavigations()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(
            new FlexQueryParameters
            {
                Include = "Orders",
                Expand = "Orders(take=1)",
                PageSize = 100
            },
            ConfigureMaps());

        result.Data.Should().HaveCount(10);

        var alice = result.Data.Single(c => c.Id == 1);
        alice.Orders.Should().HaveCount(1);
        alice.Orders.Should().OnlyContain(o => o.OrderItems == null || o.OrderItems.Count == 0);
        alice.Addresses.Should().BeNull();

        var dataQueries = DataQueries();
        dataQueries.Should().NotBeEmpty();
        dataQueries.Should().NotContain(q => q.Contains("OrderItems", StringComparison.Ordinal));
        dataQueries.Should().NotContain(q => q.Contains("Addresses", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Include_Alone_DoesNotMaterializeUnrequestedNavigations()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(
            new FlexQueryParameters
            {
                Include = "Orders",
                PageSize = 100
            },
            ConfigureMaps());

        result.Data.Should().HaveCount(10);

        var alice = result.Data.Single(c => c.Id == 1);
        alice.Orders.Should().HaveCount(2);
        alice.Orders.Should().OnlyContain(o => o.OrderItems == null || o.OrderItems.Count == 0);
        alice.Addresses.Should().BeNull();

        foreach (var id in OrderlessCustomerIds)
        {
            var customer = result.Data.Single(c => c.Id == id);
            customer.Orders.Should().BeEmpty($"customer {id} has no orders and must remain");
            customer.Addresses.Should().BeNull();
        }

        var dataQueries = DataQueries();
        dataQueries.Should().NotBeEmpty();
        dataQueries.Should().NotContain(q => q.Contains("OrderItems", StringComparison.Ordinal));
        dataQueries.Should().NotContain(q => q.Contains("Addresses", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Include_NestedPath_MaterializesRequestedNestedNavigation()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(
            new FlexQueryParameters
            {
                Include = "Orders,Orders.OrderItems",
                PageSize = 100
            },
            ConfigureMaps());

        var alice = result.Data.Single(c => c.Id == 1);
        var order10001 = alice.Orders.Single(o => o.Id == 10001);
        order10001.OrderItems.Should().HaveCount(2);
        alice.Addresses.Should().BeNull();

        var dataQueries = DataQueries();
        dataQueries.Should().NotBeEmpty();
        dataQueries.Should().Contain(q => q.Contains("OrderItems", StringComparison.Ordinal));
        dataQueries.Should().NotContain(q => q.Contains("Addresses", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Include_SiblingNavigation_MaterializesOnlyRequestedGraph()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(
            new FlexQueryParameters
            {
                Include = "Orders,Addresses",
                PageSize = 100
            },
            ConfigureMaps());

        var alice = result.Data.Single(c => c.Id == 1);
        alice.Orders.Should().HaveCount(2);
        alice.Addresses.Should().NotBeNull();
        alice.Addresses.Should().ContainSingle(a => a.Id == 1);
        alice.Orders.Should().OnlyContain(o => o.OrderItems == null || o.OrderItems.Count == 0);

        var dataQueries = DataQueries();
        dataQueries.Should().NotBeEmpty();
        dataQueries.Should().Contain(q => q.Contains("Addresses", StringComparison.Ordinal));
        dataQueries.Should().NotContain(q => q.Contains("OrderItems", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Include_NestedPath_WithWindows_MaterializesRequestedGraph_WithWindowsApplied()
    {
        using var db = SharedTestDbContext.CreateInMemorySeeded();
        db.Orders.AddRange(
            new Order
            {
                Id = 10006, CustomerId = 2, Status = "Delivered", Total = 10m, Number = "SO-EXP-1",
                OrderDate = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                OrderItems = [new OrderItem { Id = 10, OrderId = 10006, Quantity = 1, Price = 5m, Sku = "SKU-E10" }]
            },
            new Order
            {
                Id = 10005, CustomerId = 2, Status = "Delivered", Total = 20m, Number = "SO-EXP-2",
                OrderDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                OrderItems = [new OrderItem { Id = 12, OrderId = 10005, Quantity = 2, Price = 10m, Sku = "SKU-E12" }]
            });
        db.SaveChanges();

        var result = await db.Customers.FlexQueryAsync<Customer, CustomerDto>(
            new FlexQueryParameters
            {
                Include = "Orders,Orders.OrderItems",
                Expand = "Orders(take=1;filter=Status:eq:Delivered;sort=Id:desc),Orders.OrderItems(take=5)",
                PageSize = 100
            },
            ConfigureMaps());

        result.Data.Should().HaveCount(10);

        var bob = result.Data.Single(c => c.Id == 2);
        var bobOrder = bob.Orders.Should().ContainSingle().Subject;
        bobOrder.Id.Should().Be(10006);
        bobOrder.OrderItems.Select(i => i.Id).Should().Equal([10]);

        var alice = result.Data.Single(c => c.Id == 1);
        alice.Orders.Should().BeEmpty();
        alice.Addresses.Should().BeNull();
    }
}
