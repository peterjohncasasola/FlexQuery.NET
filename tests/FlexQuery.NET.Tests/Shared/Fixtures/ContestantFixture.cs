using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Tests.Shared.Fixtures;

/// <summary>
/// Contract fixture for the DTO-aware query surface tests.
/// <c>ContestantResponse.ContestantName</c> → <c>Contestant.Name</c> (MapField),
/// <c>Id</c>/<c>Age</c>/<c>Score</c> map by same-name convention, and
/// <c>Contestant.InternalNotes</c> is never exposed.
/// </summary>
public class Contestant
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int Age { get; set; }
    public decimal Score { get; set; }
    public string InternalNotes { get; set; } = null!;
}

public class ContestantDbContext : DbContext
{
    public ContestantDbContext(DbContextOptions<ContestantDbContext> options) : base(options) { }

    public DbSet<Contestant> Contestants => Set<Contestant>();

    public static ContestantDbContext Create()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ContestantDbContext>()
            .UseSqlite(connection)
            .Options;

        var ctx = new ContestantDbContext(options);
        ctx.Database.EnsureCreated();
        Seed(ctx);
        return ctx;
    }

    private static void Seed(ContestantDbContext ctx)
    {
        ctx.Contestants.AddRange(
            new Contestant { Id = 1, Name = "John", Age = 25, Score = 95.5m, InternalNotes = "n1" },
            new Contestant { Id = 2, Name = "Jane", Age = 31, Score = 88.0m, InternalNotes = "n2" },
            new Contestant { Id = 3, Name = "Bob", Age = 19, Score = 72.25m, InternalNotes = "n3" });
        ctx.SaveChanges();
    }
}
