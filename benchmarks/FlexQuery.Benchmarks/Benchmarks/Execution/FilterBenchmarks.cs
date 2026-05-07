using BenchmarkDotNet.Attributes;
using FlexQuery.Benchmarks.Abstractions;
using FlexQuery.Benchmarks.Models;
using FlexQuery.NET.Extensions;
using FlexQuery.NET.Models;
using Gridify;
using Sieve.Models;
using Sieve.Services;
using System.Collections.Generic;
using System.Linq;
using FlexQuery.NET;

namespace FlexQuery.Benchmarks.Benchmarks.Execution;

public class FilterBenchmarks : ExecutionBenchmarkBase
{
    private SieveProcessor _sieveProcessor = null!;

    [Params("active", "inactive")]
    public string Status { get; set; } = "active";

    public override void Setup()
    {
        base.Setup();
        _sieveProcessor = new SieveProcessor(Microsoft.Extensions.Options.Options.Create(new SieveOptions()));
    }

    [Benchmark(Baseline = true)]
    public List<User> Handwritten_Linq()
    {
        return DbContext.Users
            .Where(x => x.Status == Status && x.Age > 25)
            .ToList();
    }

    [Benchmark]
    public List<User> FlexQuery_Filter()
    {
        var parameters = new FlexQueryParameters
        {
            Filter = $"status:eq:{Status},age:gt:25"
        };
        var options = FlexQuery.NET.Parsers.QueryOptionsParser.Parse(parameters);
        return DbContext.Users.ApplyFilter(options).ToList();
    }

    [Benchmark]
    public List<User> Gridify_Filter()
    {
        var query = new GridifyQuery
        {
            Filter = $"status={Status},age>25"
        };
        return DbContext.Users.ApplyFiltering(query).ToList();
    }

    [Benchmark]
    public List<User> Sieve_Filter()
    {
        var model = new SieveModel
        {
            Filters = $"Status=={Status},Age>25"
        };
        return _sieveProcessor.Apply(model, DbContext.Users).ToList();
    }
}
