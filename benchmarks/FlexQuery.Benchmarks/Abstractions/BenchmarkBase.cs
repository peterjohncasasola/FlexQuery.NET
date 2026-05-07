using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using FlexQuery.Benchmarks.Infrastructure;
using FlexQuery.Benchmarks.Infrastructure.Database;
using FlexQuery.Benchmarks.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using System;

namespace FlexQuery.Benchmarks.Abstractions;

/// <summary>
/// Base class for microbenchmarks that don't require a full API pipeline.
/// </summary>
// [Config(typeof(CustomBenchmarkConfig))]
[MemoryDiagnoser]
public abstract class BenchmarkBase
{
    protected BenchmarkDbContext DbContext = null!;

    [GlobalSetup]
    public virtual void Setup()
    {
        var options = new DbContextOptionsBuilder<BenchmarkDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        DbContext = new BenchmarkDbContext(options);
        
        // Seed a standard medium dataset for microbenchmarks
        DataSeeder.Seed(DbContext, userCount: 1000);
    }

    [GlobalCleanup]
    public virtual void Cleanup()
    {
        DbContext.Database.EnsureDeleted();
        DbContext.Dispose();
    }
}
