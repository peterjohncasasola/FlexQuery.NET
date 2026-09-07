using System.Data;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FlexQuery.NET.Tests.Dapper.Integration;

/// <summary>
/// Expand-block field rewriting: filter and sort fields inside
/// <c>expand=Orders(...),Orders.OrderItems(...)</c> blocks resolve against the
/// navigation element's registered TypeMap — ForMember renames such as
/// <c>OrderResponse.DeliveryDate ← Order.ExpectedDeliveryDate</c> translate to
/// entity column names before SQL generation, and a field that exists on neither
/// the element map nor the element entity fails with a precise validation error
/// instead of an "Invalid column name" database error.
/// </summary>
[Collection("GlobalMapping")]
public class DapperExpandFieldRewriteTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CapturingLoggerFactory _loggerFactory = new();

    public DapperExpandFieldRewriteTests()
    {
        global::FlexQuery.NET.Parsers.Fql.Fql.Register();

        var ctx = SharedTestDbContext.CreateSqlite();
        SampleData.Seed(ctx);
        _connection = (SqliteConnection)ctx.Database.GetDbConnection();
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    // DTO graph mirroring the reported model: DeliveryDate ← ExpectedDeliveryDate
    // and Amount ← Total via ForMember; OrderItems stays entity-typed.

    public class OrderItemResponse
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
    }

    public class OrderResponse
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? DeliveryDate { get; set; }
        public decimal Amount { get; set; }
        public List<OrderItemResponse> OrderItems { get; set; } = [];
    }

    public class CustomerResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<OrderResponse> Orders { get; set; } = [];
    }

    private static Action<DapperQueryOptions> Maps => cfg =>
    {
        cfg.QuerySyntax = global::FlexQuery.NET.Parsers.QuerySyntax.Fql;
        cfg.CreateMap<Customer, CustomerResponse>();
        cfg.CreateMap<Order, OrderResponse>()
            .ForMember(x => x.DeliveryDate, e => e.ExpectedDeliveryDate)
            .ForMember(x => x.Amount, e => e.Total);
        cfg.CreateMap<OrderItem, OrderItemResponse>();
        cfg.LoggerFactory = _staticLoggerFactory.Value;
    };

    private static readonly Lazy<CapturingLoggerFactory> _staticLoggerFactory = new(() => new CapturingLoggerFactory());

    private CapturingLoggerFactory Logs => _staticLoggerFactory.Value;
    private void ClearLogs() => Logs.Entries.Clear();

    [Fact]
    public async Task ExpandSort_RenamedDtoField_TranslatesToEntityColumn()
    {
        ClearLogs();

        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Include = "Orders",
                Expand = "Orders(take=1;filter=Status=\"Delivered\";sort=DeliveryDate DESC)",
                Page = 1,
                PageSize = 10
            },
            Maps);

        // Bob (2) holds the only Delivered order in the seed — hydrated through the
        // filtered+sorted parent window; every other customer's window stays empty.
        var bob = result.Data.Single(c => c.Id == 2);
        bob.Orders.Should().ContainSingle();
        bob.Orders[0].Status.Should().Be("Delivered");
        result.Data.Where(c => c.Id != 2).Should().OnlyContain(c => c.Orders.Count == 0);

        // The split-query SQL must reference the ENTITY column, not the DTO name.
        // Quoted-name matching keeps "ExpectedDeliveryDate" from self-matching.
        var sql = string.Join("\n", Logs.Entries);
        sql.Should().Contain("\"Orders\".\"ExpectedDeliveryDate\" DESC");
        sql.Should().NotContain("\"DeliveryDate\"");
    }

    [Fact]
    public async Task ExpandFilter_RenamedDtoField_TranslatesToEntityColumn()
    {
        ClearLogs();

        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Include = "Orders",
                Expand = "Orders(filter=Amount=\"60\")",
                Page = 1,
                PageSize = 10
            },
            Maps);

        var sql = string.Join("\n", Logs.Entries);
        sql.Should().Contain("\"Total\"");
        sql.Should().NotContain("\"Amount\"");
    }

    [Fact]
    public async Task ExpandSort_EntityFieldName_StillWorks()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Include = "Orders",
                Expand = "Orders(take=1;filter=Status=\"Delivered\";sort=ExpectedDeliveryDate DESC)",
                Page = 1,
                PageSize = 10
            },
            Maps);

        var bob = result.Data.Single(c => c.Id == 2);
        bob.Orders.Should().ContainSingle();
        result.Data.Where(c => c.Id != 2).Should().OnlyContain(c => c.Orders.Count == 0);
    }

    [Fact]
    public async Task ExpandSort_FieldFromWrongLevel_ThrowsClearError()
    {
        // DeliveryDate/OrderDate belong to Order — not to the OrderItems element the
        // Orders.OrderItems block expands. Must fail with a precise expand-surface
        // error, not "Invalid column name" from the database.
        var ex = await Assert.ThrowsAsync<FlexQueryException>(async () =>
            await _connection.FlexQueryAsync<Customer, CustomerResponse>(
                new FlexQueryParameters
                {
                    Include = "Orders,Orders.OrderItems",
                    Expand = "Orders,Orders.OrderItems(take=5;sort=DeliveryDate DESC)",
                    Page = 1,
                    PageSize = 10
                },
                Maps));

        ex.Message.Should().Contain("DeliveryDate");
        ex.Message.Should().Contain("Orders.OrderItems");
    }

    [Fact]
    public async Task ExpandSort_UnknownField_ThrowsClearError()
    {
        var ex = await Assert.ThrowsAsync<FlexQueryException>(async () =>
            await _connection.FlexQueryAsync<Customer, CustomerResponse>(
                new FlexQueryParameters
                {
                    Include = "Orders",
                    Expand = "Orders(take=1;sort=NotARealColumn DESC)",
                    Page = 1,
                    PageSize = 10
                },
                Maps));

        ex.Message.Should().Contain("NotARealColumn");
        ex.Message.Should().Contain("Orders");
    }

    /// <summary>Captures formatted log entries so tests can assert on emitted SQL.</summary>
    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        public List<string> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void AddProvider(ILoggerProvider provider) { }

        public void Dispose() { }
    }

    private sealed class CapturingLogger(List<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => entries.Add(formatter(state, exception));
    }
}
