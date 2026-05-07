using FlexQuery.Benchmarks.Infrastructure.Database;
using FlexQuery.Benchmarks.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.Benchmarks.Infrastructure;

public static class BenchmarkDbSetup
{
    public static void Initialize(int userCount = 100000)
    {
        Console.WriteLine("Initializing SQL Server Benchmark Database...");
        
        using var db = BenchmarkDbContextFactory.Create();
        
        Console.WriteLine("Deleting existing database...");
        db.Database.EnsureDeleted();
        
        Console.WriteLine("Creating database and schema...");
        db.Database.EnsureCreated();
        
        Console.WriteLine($"Seeding {userCount} records (this may take several minutes)...");
        DataSeeder.Seed(db, userCount);
        
        Console.WriteLine("Database initialization complete.");
    }
}
