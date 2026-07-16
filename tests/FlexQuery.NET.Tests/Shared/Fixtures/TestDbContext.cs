using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Tests.Shared.Fixtures;

public class TestDbContext(DbContextOptions<SharedTestDbContext> options) : SharedTestDbContext(options)
{
    public new static TestDbContext Create(string? dbName = null)
    {
        var opts = new DbContextOptionsBuilder<SharedTestDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new TestDbContext(opts);
    }
    
    public new static TestDbContext CreateSeeded(string? dbName = null)
    {
        var ctx = Create(dbName ?? Guid.NewGuid().ToString());
        SampleData.Seed(ctx);
        return ctx;
    }
}