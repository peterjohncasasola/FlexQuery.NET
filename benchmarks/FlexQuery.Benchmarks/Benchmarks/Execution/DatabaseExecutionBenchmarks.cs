using BenchmarkDotNet.Attributes;
using FlexQuery.Benchmarks.Infrastructure.Database;
using FlexQuery.Benchmarks.Models;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Models;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.Benchmarks.Benchmarks.Execution;

[MemoryDiagnoser]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class DatabaseExecutionBenchmarks
{
    private BenchmarkDbContext _db = null!;
    private FlexQueryParameters _simpleParams = null!;
    private FlexQueryParameters _complexParams = null!;

    [GlobalSetup]
    public void Setup()
    {
        _db = BenchmarkDbContextFactory.Create();
        
        _simpleParams = new FlexQueryParameters
        {
            Filter = "status:eq:active",
            PageSize = 100,
        };

        _complexParams = new FlexQueryParameters
        {
            Filter = "status:eq:active,age:gt:25",
            Sort = "name:asc",
            PageSize = 100
        };
    }

    [Benchmark(Baseline = true)]
    public async Task<List<User>> Handwritten_SqlExecution()
    {
        return await _db.Users
            .AsNoTracking()
            .Where(u => u.Status == "active")
            .Take(100)
            .ToListAsync();
    }

    [Benchmark]
    public async Task<QueryResult<object>> FlexQuery_SqlExecution_Simple()
    {
        return await _db.Users.AsNoTracking().FlexQueryAsync(_simpleParams);
    }

    [Benchmark]
    public async Task<QueryResult<object>> FlexQuery_SqlExecution_Complex()
    {
        return await _db.Users.AsNoTracking().FlexQueryAsync(_complexParams);
    }
}
