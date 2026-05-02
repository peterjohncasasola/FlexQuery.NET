using BenchmarkDotNet.Attributes;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Caching;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Benchmarks;

[MemoryDiagnoser]
public class ExpressionCachingBenchmarks
{
    private QueryOptions _options = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Complex query to make parsing/building non-trivial
        _options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Logic = LogicOperator.And,
                Filters = new List<FilterCondition>
                {
                    new() { Field = "Name", Operator = "contains", Value = "John" },
                    new() { Field = "Age", Operator = "gt", Value = "25" }
                },
                Groups = new List<FilterGroup>
                {
                    new()
                    {
                        Logic = LogicOperator.Or,
                        Filters = new List<FilterCondition>
                        {
                            new() { Field = "Email", Operator = "endswith", Value = "@gmail.com" },
                            new() { Field = "Email", Operator = "endswith", Value = "@outlook.com" }
                        }
                    }
                }
            }
        };

        // Pre-warm cache
        FlexQueryCacheSettings.EnableCache = true;
        ExpressionBuilder.BuildPredicate<User>(_options);
    }

    [Benchmark(Baseline = true)]
    public void WithoutCaching()
    {
        _options.EnableCache = false;
        ExpressionBuilder.BuildPredicate<User>(_options);
    }

    [Benchmark]
    public void WithCaching()
    {
        _options.EnableCache = true;
        ExpressionBuilder.BuildPredicate<User>(_options);
    }
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string Email { get; set; } = "";
}
