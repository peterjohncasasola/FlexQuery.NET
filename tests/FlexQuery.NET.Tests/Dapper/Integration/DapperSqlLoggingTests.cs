using System.Data;
using System.Data.Common;
using System.Text.Json;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Mapping;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FlexQuery.NET.Tests.Dapper.Integration;

/// <summary>
/// Observability tests for Dapper SQL execution logging: every Dapper command
/// (root query, split-query include/expand, counts, aggregates) logs its final
/// SQL and parameters immediately before execution via
/// <see cref="FlexQuery.NET.Dapper.Options.DapperQueryOptions.LoggerFactory"/>.
///
/// The regression-shape request is the same one used to debug nested DTO
/// expansion: include=Orders,Orders.OrderItems with per-level expand windows —
/// the OrderItems split-query SQL must be visible in the logs.
/// Logging must never change results or execute additional commands.
/// </summary>
[Collection("GlobalMapping")]
public class DapperSqlLoggingTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CountingSqliteConnection _countingConnection;
    private readonly CapturingLoggerFactory _loggerFactory = new();

    public DapperSqlLoggingTests()
    {
        global::FlexQuery.NET.Parsers.Fql.Fql.Register();

        FlexQueryMapping.Reset();
        FlexQueryMapping.Configure(registry =>
        {
            registry.GetOrCreate<Customer, CustomerResponse>();
            registry.GetOrCreate<Order, OrderResponse>();
            registry.GetOrCreate<OrderItem, OrderItemResponse>();
        });

        // One counting connection backs both the seeded EF context and the Dapper
        // queries — required because separate SQLite in-memory connections do not
        // share schema or data.
        _countingConnection = new CountingSqliteConnection("Filename=:memory:");
        _countingConnection.Open();

        var options = new DbContextOptionsBuilder<SharedTestDbContext>()
            .UseSqlite(_countingConnection)
            .Options;
        using var ctx = new SharedTestDbContext(options);
        ctx.Database.EnsureCreated();
        SampleData.Seed(ctx);
        _connection = _countingConnection;

        // Extra fixture rows (same shape as the nested-expansion regression fixture):
        //   Customer 2: Delivered order 10005 with items 10, 11.
        //   Customer 4: Delivered order 10007 with EIGHT items 20..27 (per-parent take proof).
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = OFF;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = """
            INSERT INTO Orders (Id, CustomerId, OrderDate, Status, Total, Price, Category, Number) VALUES
                (10005, 2, '2023-02-10', 'Delivered', 60, 0, '', 'SO-005'),
                (10007, 4, '2023-04-10', 'Delivered', 80, 0, '', 'SO-007');
            INSERT INTO OrderItems (Id, OrderId, ProductId, Quantity, Price, Sku, UnitPrice) VALUES
                (10, 10005, 1, 4, 30, 'SKU-EEE', 30),
                (11, 10005, 1, 2, 30, 'SKU-FFF', 30),
                (20, 10007, 1, 1, 10, 'SKU-P20', 10),
                (21, 10007, 1, 1, 10, 'SKU-P21', 10),
                (22, 10007, 1, 1, 10, 'SKU-P22', 10),
                (23, 10007, 1, 1, 10, 'SKU-P23', 10),
                (24, 10007, 1, 1, 10, 'SKU-P24', 10),
                (25, 10007, 1, 1, 10, 'SKU-P25', 10),
                (26, 10007, 1, 1, 10, 'SKU-P26', 10),
                (27, 10007, 1, 1, 10, 'SKU-P27', 10);
            """;
        cmd.ExecuteNonQuery();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        FlexQueryMapping.Reset();
        _connection.Dispose();
        _countingConnection.Dispose();
    }

    // DTO response graph -----------------------------------------------------------

    public class OrderItemResponse
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
    }

    public class OrderResponse
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<OrderItemResponse> OrderItems { get; set; } = [];
    }

    public class CustomerResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<OrderResponse> Orders { get; set; } = [];
    }

    // Helpers ----------------------------------------------------------------------

    private static Action<DapperQueryOptions> Fql => opt =>
        opt.QuerySyntax = global::FlexQuery.NET.Parsers.QuerySyntax.Fql;

    private static Action<DapperQueryOptions> WithLogging(CapturingLoggerFactory factory) => opt =>
    {
        opt.QuerySyntax = global::FlexQuery.NET.Parsers.QuerySyntax.Fql;
        opt.LoggerFactory = factory;
    };

    private static FlexQueryParameters RegressionParameters() => new()
    {
        Include = "Orders,Orders.OrderItems",
        Expand = "Orders(take=1;filter=Status=\"Delivered\";sort=Id DESC),Orders.OrderItems(take=5)",
        Page = 1,
        PageSize = 5
    };

    private static string Serialize(object? value)
        => JsonSerializer.Serialize(value);

    // Test 1 — entity query logs the final SQL --------------------------------------

    [Fact]
    public async Task EntityQuery_LogsFinalSql()
    {
        var result = await _connection.FlexQueryAsync<Customer>(
            new FlexQueryParameters
            {
                Filter = "Status=\"Delivered\"",
                Sort = "Id DESC",
                Page = 1,
                PageSize = 2
            },
            WithLogging(_loggerFactory));

        result.Data.Should().NotBeNull();

        var entries = _loggerFactory.Entries;
        entries.Should().NotBeEmpty();

        // The root query is logged first (count queries follow it).
        var sqlEntry = entries.First(e => e.Contains("Executing Dapper query"));
        sqlEntry.Should().Contain("SQL:");
        sqlEntry.Should().Contain("SELECT");
        sqlEntry.Should().Contain("FROM");
        sqlEntry.Should().Contain("ORDER BY");

        // The final SQL reflects filter/sort/paging: the filter parameter value appears
        // in the parameter section of the same entry.
        sqlEntry.Should().Contain("Delivered");
    }

    // Test 2 — logs generated parameters --------------------------------------------

    [Fact]
    public async Task EntityQuery_LogsGeneratedParameters()
    {
        await _connection.FlexQueryAsync<Customer>(
            new FlexQueryParameters
            {
                Filter = "Status=\"Delivered\"",
                Page = 1,
                PageSize = 2
            },
            WithLogging(_loggerFactory));

        var sqlEntry = _loggerFactory.Entries.First(e => e.Contains("Executing Dapper query"));
        sqlEntry.Should().Contain("Parameters:");

        // Parameter lines use the "@name = value" representation actually passed to Dapper.
        var parameterSection = sqlEntry[(sqlEntry.IndexOf("Parameters:", StringComparison.Ordinal))..];
        parameterSection.Should().MatchRegex("@\\w+ = Delivered");
    }

    // Test 3 — DTO query is logged ---------------------------------------------------

    [Fact]
    public async Task DtoQuery_LogsFinalSql()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Filter = "Name=\"Alice Johnson\"",
                Page = 1,
                PageSize = 2
            },
            WithLogging(_loggerFactory));

        result.Data.Should().NotBeNull();
        _loggerFactory.Entries.Should().Contain(e =>
            e.Contains("Executing Dapper query") &&
            e.Contains("SQL:") &&
            e.Contains("SELECT"));
    }

    // Test 4 — nested include/expand logs the complete SQL (OrderItems split query) --

    [Fact]
    public async Task NestedIncludeExpand_LogsCompleteSqlIncludingOrderItems()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            RegressionParameters(),
            WithLogging(_loggerFactory));

        result.Data.Should().NotBeEmpty();

        var entries = _loggerFactory.Entries
            .Where(e => e.Contains("Executing Dapper query"))
            .ToList();

        // Root query plus one split query per expand level (Orders, Orders.OrderItems)
        // — at minimum three separate Dapper commands must have been logged.
        entries.Count.Should().BeGreaterThanOrEqualTo(3);

        // The OrderItems split-query SQL is visible: the exact thing the regression
        // debugging scenario needs to inspect.
        entries.Should().Contain(e => e.Contains("SQL:") && e.Contains("OrderItems"));
        entries.Should().Contain(e => e.Contains("SQL:") && e.Contains("Orders"));
    }

    // Test 5 — logging does not execute the query more than once ---------------------

    [Fact]
    public async Task Logging_DoesNotExecuteAdditionalCommands()
    {
        var parameters = RegressionParameters();

        var counting = _countingConnection;
        counting.CommandsExecuted = 0;

        await counting.FlexQueryAsync<Customer, CustomerResponse>(parameters, Fql);
        var countWithoutLogging = counting.CommandsExecuted;

        counting.CommandsExecuted = 0;
        await counting.FlexQueryAsync<Customer, CustomerResponse>(parameters, WithLogging(_loggerFactory));
        var countWithLogging = counting.CommandsExecuted;

        countWithoutLogging.Should().BeGreaterThan(0);
        countWithLogging.Should().Be(countWithoutLogging);
    }

    // Test 6 — existing query results remain unchanged -------------------------------

    [Fact]
    public async Task Results_AreUnchanged_WithLoggingEnabled()
    {
        var parameters = RegressionParameters();

        var withoutLogging = await _connection.FlexQueryAsync<Customer, CustomerResponse>(parameters, Fql);

        var withLogging = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            parameters, WithLogging(_loggerFactory));

        Serialize(withLogging.Data).Should().Be(Serialize(withoutLogging.Data));
        withLogging.TotalCount.Should().Be(withoutLogging.TotalCount);
        withLogging.ResultCount.Should().Be(withoutLogging.ResultCount);
        withLogging.Page.Should().Be(withoutLogging.Page);
        withLogging.PageSize.Should().Be(withoutLogging.PageSize);
    }

    // Test 7 — logging disabled does not affect execution ----------------------------

    [Fact]
    public async Task LoggingDisabled_BehavesExactlyAsBefore()
    {
        var parameters = RegressionParameters();

        var counting = _countingConnection;
        counting.CommandsExecuted = 0;

        // Baseline: LoggerFactory is null (the default) — no logger is ever consulted.
        var result = await counting.FlexQueryAsync<Customer, CustomerResponse>(parameters, Fql);
        var baselineCommandCount = counting.CommandsExecuted;
        var baselinePayload = Serialize(result.Data);

        _loggerFactory.Entries.Should().BeEmpty();

        // Same request with logging enabled: identical command count and results.
        counting.CommandsExecuted = 0;
        var logged = await counting.FlexQueryAsync<Customer, CustomerResponse>(parameters, WithLogging(_loggerFactory));

        counting.CommandsExecuted.Should().Be(baselineCommandCount);
        Serialize(logged.Data).Should().Be(baselinePayload);
        _loggerFactory.Entries.Should().NotBeEmpty();
    }

    // Test infrastructure ------------------------------------------------------------

    /// <summary>Captures formatted log entries so tests can assert on the emitted SQL text.</summary>
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

    /// <summary>
    /// Counts executed SELECT statements by subscribing to SQLite's per-statement trace
    /// event, proving that logging neither adds nor duplicates database round-trips.
    /// The count is published to <see cref="DapperSqlLoggingTests._lastQueryStatementCount"/>
    /// on dispose (statements execute synchronously within the awaited request).
    /// </summary>
    /// <summary>
    /// Counts every executed command by overriding command creation. Named
    /// <c>…SqliteConnection</c> so <c>SqlDialectResolver</c> (which matches provider
    /// type names) resolves it as SQLite. Shares the same connection string as the
    /// seeded in-memory database, so both connections see the same data.
    /// </summary>
    private sealed class CountingSqliteConnection : SqliteConnection
    {
        public CountingSqliteConnection(string connectionString) : base(connectionString) { }

        public int CommandsExecuted { get; set; }

        protected override DbCommand CreateDbCommand()
        {
            var command = base.CreateDbCommand();
            command.Disposed += (_, _) => CommandsExecuted++;
            return command;
        }
    }
}
