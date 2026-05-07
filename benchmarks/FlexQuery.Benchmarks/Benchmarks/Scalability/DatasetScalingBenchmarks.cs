using BenchmarkDotNet.Attributes;
using FlexQuery.Benchmarks.Abstractions;
using FlexQuery.Benchmarks.Models;
using FlexQuery.NET.Extensions;
using FlexQuery.NET.Models;
using System.Collections.Generic;
using System.Linq;
using FlexQuery.NET;

namespace FlexQuery.Benchmarks.Benchmarks.Scalability;

public class DatasetScalingBenchmarks : ExecutionBenchmarkBase
{
    [Params(100, 1000, 10000)]
    public int DatasetSize { get; set; }

    public override void Setup()
    {
        // Custom setup to seed specific sizes
        base.Setup(); // This seeds 1000 by default, so we override
        DbContext.Users.RemoveRange(DbContext.Users);
        DbContext.SaveChanges();
        
        Infrastructure.Seed.DataSeeder.Seed(DbContext, userCount: DatasetSize);
    }

    [Benchmark]
    public List<User> FlexQuery_Scale_Filter()
    {
        var parameters = new FlexQueryParameters
        {
            Filter = "status:eq:active"
        };
        var options = FlexQuery.NET.Parsers.QueryOptionsParser.Parse(parameters);
        return DbContext.Users.ApplyFilter(options).ToList();
    }
}
