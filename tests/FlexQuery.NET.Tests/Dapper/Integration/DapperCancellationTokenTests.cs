using System.Data;
using System.Data.Common;
using Dapper;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace FlexQuery.NET.Tests.Dapper.Integration;

/// <summary>
/// Verifies that <see cref="CancellationToken"/> flows from the public FlexQueryAsync
/// overloads through to Dapper's <see cref="CommandDefinition"/> and ultimately to the
/// underlying ADO.NET command.
///
/// Strategy:
/// * Pre-cancelled tokens must surface as <see cref="OperationCanceledException"/>,
///   proving the token is wired into the actual database command execution.
/// * Normal execution with <see cref="CancellationToken.None"/> must remain unchanged.
/// * All major Dapper execution paths are covered: entity query, DTO query, aggregate,
///   count, and split-query include loading.
/// </summary>
public class DapperCancellationTokenTests : IDisposable
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly SqliteConnection _connection;
    private readonly SqlProjectionDbContext _db;

    public DapperCancellationTokenTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        global::FlexQuery.NET.Parsers.Fql.Fql.Register();

        var connection = new SqliteConnection("Data Source=FlexQueryTest;Mode=Memory;Cache=Shared");
        connection.Open();

        var options = new DbContextOptionsBuilder<SharedTestDbContext>()
            .UseSqlite(connection)
            .EnableSensitiveDataLogging()
            .Options;

        _db = new SqlProjectionDbContext(options, connection);
        _db.Database.EnsureCreated();
        SampleData.Seed(_db);
        _connection = connection;
    }

    public void Dispose()
    {
        _connection.Dispose();
        _db.Dispose();
    }

    private static Action<DapperQueryOptions> Fql => opt =>
        opt.QuerySyntax = global::FlexQuery.NET.Parsers.QuerySyntax.Fql;

    // -------------------------------------------------------------------------
    // Entity query path (ExecuteAsync -> connection.QueryAsync)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EntityQuery_WithNoneToken_ReturnsResults()
    {
        var result = await _connection.FlexQueryAsync<Customer>(
            new FlexQueryParameters { Filter = "Id = 1" },
            Fql,
            CancellationToken.None);

        result.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task EntityQuery_WithPreCancelledToken_ThrowsOperationCanceled()
    {
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _connection.FlexQueryAsync<Customer>(
                new FlexQueryParameters { Filter = "Id = 1" },
                Fql,
                cts.Token));
    }

    // -------------------------------------------------------------------------
    // DTO query path (RunDtoAsync -> connection.QueryAsync)
    // -------------------------------------------------------------------------

    public class CustomerNameDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public async Task DtoQuery_WithNoneToken_ReturnsResults()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerNameDto>(
            new FlexQueryParameters { Filter = "Id = 1" },
            Fql,
            CancellationToken.None);

        result.Data.Should().NotBeEmpty();
        result.Data[0].Name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DtoQuery_WithPreCancelledToken_ThrowsOperationCanceled()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _connection.FlexQueryAsync<Customer, CustomerNameDto>(
                new FlexQueryParameters { Filter = "Id = 1" },
                Fql,
                cts.Token));
    }

    // -------------------------------------------------------------------------
    // Aggregate path (AggregateEvaluator -> connection.QueryFirstOrDefaultAsync)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AggregateQuery_WithNoneToken_ReturnsAggregates()
    {
        var result = await _connection.FlexQueryAsync<Customer>(
            new FlexQueryParameters { Aggregate = "count:Id:total" });

        result.Aggregates.Should().NotBeNull();
        result.Aggregates!["Id"]["count"].Should().NotBeNull();
    }

    [Fact]
    public async Task AggregateQuery_WithPreCancelledToken_ThrowsOperationCanceled()
    {
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _connection.FlexQueryAsync<Customer>(
                new FlexQueryParameters { Aggregate = "count:Id:total" },
                cancellationToken: cts.Token));
    }

    // -------------------------------------------------------------------------
    // Count path (CountEvaluator -> connection.QuerySingleAsync)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CountQuery_WithNoneToken_ReturnsResults()
    {
        var result = await _connection.FlexQueryAsync<Customer>(
            new FlexQueryParameters { Filter = "Id = 1", PageSize = 10 },
            Fql,
            CancellationToken.None);

        result.Data.Should().NotBeEmpty();
        result.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CountQuery_WithPreCancelledToken_ThrowsOperationCanceled()
    {
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _connection.FlexQueryAsync<Customer>(
                new FlexQueryParameters { Filter = "Id = 1", PageSize = 10 },
                Fql,
                cts.Token));
    }

    // -------------------------------------------------------------------------
    // Split-query include path (DapperRowHydrator -> connection.QueryAsync)
    // -------------------------------------------------------------------------

    public class CustomerWithOrdersDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<Order>? Orders { get; set; }
    }

    [Fact]
    public async Task IncludeQuery_WithNoneToken_ReturnsResultsWithNavigation()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerWithOrdersDto>(
            new FlexQueryParameters { Include = "Orders", Filter = "Id = 1" },
            opt =>
            {
                opt.QuerySyntax = global::FlexQuery.NET.Parsers.QuerySyntax.Fql;
                opt.UseModel(SharedFlexQueryModel.Instance);
            },
            CancellationToken.None);

        result.Data.Should().NotBeEmpty();
        result.Data[0].Orders.Should().NotBeNull();
    }

    [Fact]
    public async Task IncludeQuery_WithPreCancelledToken_ThrowsOperationCanceled()
    {
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _connection.FlexQueryAsync<Customer, CustomerWithOrdersDto>(
                new FlexQueryParameters { Include = "Orders", Filter = "Id = 1" },
                opt =>
                {
                    opt.QuerySyntax = global::FlexQuery.NET.Parsers.QuerySyntax.Fql;
                    opt.UseModel(SharedFlexQueryModel.Instance);
                },
                cts.Token));
    }

    // -------------------------------------------------------------------------
    // Token reaches Dapper CommandDefinition via a command-intercepting connection
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EntityQuery_TokenIsPassedToUnderlyingCommand()
    {
        var capturedTokens = new List<CancellationToken>();
        var cts = new CancellationTokenSource();

        await using var intercepted = new TokenCapturingSqliteConnection(_connection.ConnectionString, capturedTokens);
        if (intercepted.State != ConnectionState.Open) intercepted.Open();

        await intercepted.FlexQueryAsync<Customer>(
            new FlexQueryParameters { Filter = "Id = 1" },
            Fql,
            cts.Token);

        capturedTokens.Should().HaveCount(2);
        for (var i = 0; i < capturedTokens.Count; i++)
        {
            _testOutputHelper.WriteLine($"Token[{i}]: CanBeCanceled={capturedTokens[i].CanBeCanceled}, IsCancellationRequested={capturedTokens[i].IsCancellationRequested}, GetHashCode={capturedTokens[i].GetHashCode()}");
            _testOutputHelper.WriteLine($"cts.Token: CanBeCanceled={cts.Token.CanBeCanceled}, IsCancellationRequested={cts.Token.IsCancellationRequested}, GetHashCode={cts.Token.GetHashCode()}");
            capturedTokens[i].Should().Be(cts.Token);
        }
    }

    [Fact]
    public async Task EntityQuery_PreCancelledToken_SurfaceAsOperationCanceled()
    {
        var capturedTokens = new List<CancellationToken>();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        using var intercepted = new TokenCapturingSqliteConnection(_connection.ConnectionString, capturedTokens);
        if (intercepted.State != ConnectionState.Open) intercepted.Open();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await intercepted.FlexQueryAsync<Customer>(
                new FlexQueryParameters { Filter = "Id = 1" },
                Fql,
                cts.Token));

        capturedTokens.Should().ContainSingle();
        capturedTokens[0].Should().Be(cts.Token);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private sealed class TokenCapturingSqliteConnection(
        string connectionString,
        List<CancellationToken> capturedTokens)
        : SqliteConnection(connectionString)
    {
        private readonly List<CancellationToken> _capturedTokens = capturedTokens;

        protected override DbCommand CreateDbCommand()
        {
            var innerCommand = base.CreateDbCommand();
            return new CapturingCommand(innerCommand, _capturedTokens);
        }
    }

    private sealed class CapturingCommand(
        DbCommand inner,
        List<CancellationToken> capturedTokens)
        : DbCommand
    {
        private readonly DbCommand _inner = inner;

        public override string CommandText
        {
            get => _inner.CommandText;
            set => _inner.CommandText = value!;
        }

        public override int CommandTimeout
        {
            get => _inner.CommandTimeout;
            set => _inner.CommandTimeout = value;
        }

        public override CommandType CommandType
        {
            get => _inner.CommandType;
            set => _inner.CommandType = value;
        }

        public override bool DesignTimeVisible
        {
            get => _inner.DesignTimeVisible;
            set => _inner.DesignTimeVisible = value;
        }

        public override UpdateRowSource UpdatedRowSource
        {
            get => _inner.UpdatedRowSource;
            set => _inner.UpdatedRowSource = value;
        }

        protected override DbConnection? DbConnection
        {
            get => _inner.Connection;
            set => _inner.Connection = (DbConnection?)value;
        }

        protected override DbParameterCollection DbParameterCollection => _inner.Parameters;
        protected override DbTransaction? DbTransaction
        {
            get => _inner.Transaction;
            set => _inner.Transaction = (DbTransaction?)value;
        }

        public override void Cancel() => _inner.Cancel();
        public override void Prepare() => _inner.Prepare();

        public override int ExecuteNonQuery() => _inner.ExecuteNonQuery();
        public override object? ExecuteScalar() => _inner.ExecuteScalar();

        protected override DbParameter CreateDbParameter() => _inner.CreateParameter();

        protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(
            CommandBehavior behavior, CancellationToken cancellationToken)
        {
            capturedTokens.Add(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return await _inner.ExecuteReaderAsync(behavior, cancellationToken);
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
            => throw new NotSupportedException("Synchronous execution is not supported by the interceptor.");
    }
}
