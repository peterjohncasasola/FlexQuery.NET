using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace FlexQuery.NET.Tests.Integration;

public class CollectionAnySqliteTests
{
    // Guards collection Any filters against EF Core translation failures caused by null-guarded collection navigation expressions.
    [Fact]
    public void Filter_DslAny_OnManyToManyCollectionNavigation_IsTranslatableBySqlite()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<CollectionAnyDbContext>()
            .UseSqlite(connection)
            .Options;

        using var db = new CollectionAnyDbContext(options);
        db.Database.EnsureCreated();
        db.Movies.AddRange(
            new Movie
            {
                Id = 1,
                Title = "Matching movie",
                Countries = [new Country { Id = 14, Name = "Netherlands" }]
            },
            new Movie
            {
                Id = 2,
                Title = "Other movie",
                Countries = [new Country { Id = 15, Name = "Belgium" }]
            });
        db.SaveChanges();

        // Regression: collection Any filters must translate for EF Core relational providers.
        // The previous expression shape added a collection-navigation null guard, producing
        // `MaterializeCollectionNavigation(...) != null && ...Any(...)`, which SQLite could not translate.
        var opts = Parse(new()
        {
            ["filter"] = "countries:any:id:eq:14"
        });
        opts.Paging.Disabled = true;

        var result = db.Movies.Apply(opts).ToList();

        result.Should().ContainSingle();
        result[0].Title.Should().Be("Matching movie");
    }

    private static QueryOptions Parse(Dictionary<string, string> raw)
    {
        var kvps = raw.ToDictionary(
            kv => kv.Key,
            kv => new StringValues(kv.Value),
            StringComparer.OrdinalIgnoreCase);
        return QueryOptionsParser.Parse(kvps);
    }

    private sealed class CollectionAnyDbContext(DbContextOptions<CollectionAnyDbContext> options) : DbContext(options)
    {
        public DbSet<Movie> Movies => Set<Movie>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Movie>()
                .HasMany(x => x.Countries)
                .WithMany(x => x.Movies);
        }
    }

    private sealed class Movie
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public List<Country> Countries { get; set; } = [];
    }

    private sealed class Country
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public List<Movie> Movies { get; set; } = [];
    }
}
