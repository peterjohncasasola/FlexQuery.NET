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
        public List<OrderItemDto> OrderItems { get; set; } = [];
    }

    public class CustomerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<OrderDto> Orders { get; set; } = [];
    }

    private static Action<FlexQuery.NET.EntityFrameworkCore.Options.EfCoreQueryOptions> ConfigureMaps()
        => opt =>
        {
            opt.CreateMap<Customer, CustomerDto>()
                .ForNavigation(d => d.Orders, e => e.Orders);
            opt.CreateMap<Order, OrderDto>();
            opt.CreateMap<OrderItem, OrderItemDto>();
        };

    private List<string> DataQueries()
        => _sql
            .Where(s => s.Contains("SELECT", StringComparison.Ordinal)
                        && s.Contains("Customers", StringComparison.Ordinal)
                        && s.Contains("Orders", StringComparison.Ordinal))
            .ToList();

    [Fact]
    public async Task Include_WithExpandTake_DoesNotMaterializeUnrequestedNestedNavigation()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(
            new FlexQueryParameters
            {
                Include = "Orders",
                Expand = "Orders(take=1)",
                PageSize = 100
            },
            ConfigureMaps());

        // Alice (id=1) has 2 orders; the take applies per parent.
        var alice = result.Data.Single(c => c.Id == 1);
        alice.Orders.Should().HaveCount(1);

        // The declared-but-unrequested nested navigation must NOT be materialized.
        alice.Orders.Should().OnlyContain(o => o.OrderItems == null || o.OrderItems.Count == 0,
            "OrderItems was not included — the DTO-declared nested navigation must stay at its default");

        // And it must never enter the SQL: no OrderItems columns in the data query.
        var dataQueries = DataQueries();
        dataQueries.Should().NotBeEmpty();
        dataQueries.Should().NotContain(q => q.Contains("OrderItems", StringComparison.Ordinal),
            "the generated SQL must only cover the requested navigation graph");
    }

    [Fact]
    public async Task Include_Alone_DoesNotMaterializeUnrequestedNestedNavigation()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(
            new FlexQueryParameters
            {
                Include = "Orders",
                PageSize = 100
            },
            ConfigureMaps());

        var alice = result.Data.Single(c => c.Id == 1);
        alice.Orders.Should().HaveCount(2);
        alice.Orders.Should().OnlyContain(o => o.OrderItems == null || o.OrderItems.Count == 0,
            "OrderItems was not included — the DTO-declared nested navigation must stay at its default");

        var dataQueries = DataQueries();
        dataQueries.Should().NotBeEmpty();
        dataQueries.Should().NotContain(q => q.Contains("OrderItems", StringComparison.Ordinal),
            "the generated SQL must only cover the requested navigation graph");
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

        // Explicitly included: the nested graph is materialized.
        var alice = result.Data.Single(c => c.Id == 1);
        var order10001 = alice.Orders.Single(o => o.Id == 10001);
        order10001.OrderItems.Should().HaveCount(2);

        var dataQueries = DataQueries();
        dataQueries.Should().NotBeEmpty();
        dataQueries.Should().Contain(q => q.Contains("OrderItems", StringComparison.Ordinal),
            "OrderItems was explicitly included and must be queried");
    }
}
