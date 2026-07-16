using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Tests.Shared.Fixtures;

internal class SqlProjectionDbContext(DbContextOptions<SharedTestDbContext> options, SqliteConnection connection)
    : SharedTestDbContext(options)
{
    public static SqlProjectionDbContext CreateSeeded()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<SharedTestDbContext>()
            .UseSqlite(connection)
            .EnableSensitiveDataLogging()
            .Options;

        var context = new SqlProjectionDbContext(options, connection);
        context.Database.EnsureCreated();
        SampleData.Seed(context);
        return context;
    }

    public override void Dispose()
    {
        connection.Dispose();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await connection.DisposeAsync();
    }
}