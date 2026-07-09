#pragma warning disable CS8764

using System.Data;
using System.Data.Common;
using FlexQuery.NET.Dapper.Dialects;

namespace FlexQuery.NET.Tests.Dapper.Dialects;

public class SqlDialectResolverTests
{
    [Fact]
    public void SqlConnection_Resolves_SqlServerDialect()
    {
        var dialect = SqlDialectResolver.Resolve(new FakeSqlConnection());
        dialect.Should().BeOfType<SqlServerDialect>();
    }

    [Fact]
    public void NpgsqlConnection_Resolves_PostgreSqlDialect()
    {
        var dialect = SqlDialectResolver.Resolve(new FakeNpgsqlConnection());
        dialect.Should().BeOfType<PostgreSqlDialect>();
    }

    [Fact]
    public void SqliteConnection_Resolves_SqliteDialect()
    {
        var dialect = SqlDialectResolver.Resolve(new FakeSqliteConnection());
        dialect.Should().BeOfType<SqliteDialect>();
    }

    [Fact]
    public void MySqlConnection_Resolves_MySqlDialect()
    {
        var dialect = SqlDialectResolver.Resolve(new FakeMySqlConnection());
        dialect.Should().BeOfType<MySqlDialect>();
    }

    [Fact]
    public void MariaDbConnection_Resolves_MariaDbDialect()
    {
        var dialect = SqlDialectResolver.Resolve(new FakeMariaDbConnection());
        dialect.Should().BeOfType<MariaDbDialect>();
    }

    [Fact]
    public void OracleConnection_Resolves_OracleDialect()
    {
        var dialect = SqlDialectResolver.Resolve(new FakeOracleConnection());
        dialect.Should().BeOfType<OracleDialect>();
    }

    [Fact]
    public void UnknownConnectionType_Throws_NotSupportedException()
    {
        var act = () => SqlDialectResolver.Resolve(new FakeUnknownConnection());
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*not a supported database provider*");
    }

    // ──────────────────────────────────────────────────────────────
    //  Fake DbConnection types — only the class name matters for
    //  SqlDialectResolver.Resolve(), which uses GetType().Name.
    // ──────────────────────────────────────────────────────────────

    private sealed class FakeSqlConnection : DbConnection
    {
        public override string? ConnectionString { get; set; } = string.Empty;
        public override string? Database => "Test";
        public override string? DataSource => ":memory:";
        public override string? ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Closed;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => null!;
        protected override DbCommand CreateDbCommand() => null!;
    }

    private sealed class FakeNpgsqlConnection : DbConnection
    {
        public override string? ConnectionString { get; set; } = string.Empty;
        public override string? Database => "Test";
        public override string? DataSource => "localhost";
        public override string? ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Closed;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => null!;
        protected override DbCommand CreateDbCommand() => null!;
    }

    private sealed class FakeSqliteConnection : DbConnection
    {
        public override string? ConnectionString { get; set; } = string.Empty;
        public override string? Database => "Test";
        public override string? DataSource => ":memory:";
        public override string? ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Closed;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => null!;
        protected override DbCommand CreateDbCommand() => null!;
    }

    private sealed class FakeMySqlConnection : DbConnection
    {
        public override string? ConnectionString { get; set; } = string.Empty;
        public override string? Database => "Test";
        public override string? DataSource => "localhost";
        public override string? ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Closed;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => null!;
        protected override DbCommand CreateDbCommand() => null!;
    }

    private sealed class FakeMariaDbConnection : DbConnection
    {
        public override string? ConnectionString { get; set; } = string.Empty;
        public override string? Database => "Test";
        public override string? DataSource => "localhost";
        public override string? ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Closed;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => null!;
        protected override DbCommand CreateDbCommand() => null!;
    }

    private sealed class FakeOracleConnection : DbConnection
    {
        public override string? ConnectionString { get; set; } = string.Empty;
        public override string? Database => "Test";
        public override string? DataSource => "localhost";
        public override string? ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Closed;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => null!;
        protected override DbCommand CreateDbCommand() => null!;
    }

    private sealed class FakeUnknownConnection : DbConnection
    {
        public override string? ConnectionString { get; set; } = string.Empty;
        public override string? Database => "Test";
        public override string? DataSource => "unknown";
        public override string? ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Closed;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => null!;
        protected override DbCommand CreateDbCommand() => null!;
    }
}
