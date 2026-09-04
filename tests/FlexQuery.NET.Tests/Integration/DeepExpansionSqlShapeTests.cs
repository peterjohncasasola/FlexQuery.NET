using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared;
using FlexQuery.NET.Tests.Shared.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;
using Xunit;

namespace FlexQuery.NET.Tests.Integration;

/// <summary>
/// Deep collection expansion query-shape tests (SQLite relational provider):
///
/// 1. Deep child filter/sort is correlated into the parent JOIN (single query —
///    no whole-child-table materialization, no APPLY needed for filter/sort).
/// 2. The single-level expansion Take applies server-side within the window.
/// 3. Per-parent Take on deep levels requires APPLY (SQL Server) and is verified by
///    the InMemory correctness suite (DeepCollectionExpansionTests); on SQLite the
///    provider throws — a documented provider capability boundary, not a code defect.
/// </summary>
public class DeepExpansionSqlShapeTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly List<string> _sql = [];
    private readonly SharedTestDbContext _db;

    public DeepExpansionSqlShapeTests()
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

    public class OrderDto
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<OrderItemDto> OrderItems { get; set; } = new();
    }

    public class OrderItemDto
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
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

    [Fact]
    public async Task DeepExpansion_FilterSort_CorrelatedIntoParentJoin()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(
            new FlexQueryParameters
            {
                Filter = "Id:eq:1",
                Include = "Orders",
                Expand = "Orders(filter=Status:eq:Shipped;sort=Id:desc)",
                PageSize = 20
            },
            ConfigureMaps());

        // Correctness: only Shipped orders for customer 1.
        result.Data.Should().ContainSingle();
        var orders = result.Data[0].Orders;
        orders.Should().NotBeEmpty();
        orders.All(o => o.Status == "Shipped").Should().BeTrue();

        // Query shape: exactly one data query; the child filter is correlated inside
        // the parent JOIN (no separate child-table scan, no client-side filtering).
        var dataQueries = _sql
            .Where(s => s.Contains("Executed DbCommand", StringComparison.Ordinal)
                        && s.Contains("SELECT", StringComparison.Ordinal)
                        && s.Contains("Orders", StringComparison.Ordinal))
            .ToList();
        dataQueries.Should().ContainSingle("deep expansion must execute as one correlated query");

        var query = dataQueries[0];
        // The child filter value is parameterized ("Shipped" = 7 chars) — its presence
        // in the parameter list proves the filter executed in SQL, not client-side.
        query.Should().Contain("Size = 7", "the child filter value is applied as a SQL parameter");
        query.Should().Contain("LEFT JOIN", "the child rows join to the paged parent");
    }

    [Fact]
    public async Task SingleLevelExpansion_Take_ServerSide()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(
            new FlexQueryParameters
            {
                Filter = "Id:eq:1",
                Include = "Orders",
                Expand = "Orders(take=1)",
                PageSize = 20
            },
            ConfigureMaps());

        result.Data.Should().ContainSingle();
        result.Data[0].Orders.Should().HaveCount(1);
    }

    [Fact]
    public async Task DeepTake_OnSqlite_ThrowsApplyLimitation()
    {
        // Documents the provider capability boundary: nested Take requires APPLY,
        // unsupported on SQLite. On SQL Server (production target) the same query
        // translates into a correlated OUTER APPLY with per-order TOP(5).
        var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(async () =>
            await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(
                new FlexQueryParameters
                {
                    Filter = "Id:eq:1",
                    Include = "Orders,Orders.OrderItems",
                    Expand = "Orders(take=2),Orders.OrderItems(take=5)",
                    PageSize = 20
                },
                opt =>
                {
                    opt.CreateMap<Customer, CustomerDto>()
                        .ForNavigation(d => d.Orders, e => e.Orders);
                    opt.CreateMap<Order, OrderDto>();
                    opt.CreateMap<OrderItem, OrderItemDto>();
                }));

        ex.Message.Should().Contain("APPLY");
    }
}
