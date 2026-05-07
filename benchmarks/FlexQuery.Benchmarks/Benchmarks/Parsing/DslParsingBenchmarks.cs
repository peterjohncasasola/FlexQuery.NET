using BenchmarkDotNet.Attributes;
using FlexQuery.Benchmarks.Abstractions;
using FlexQuery.NET.Parsers.Dsl;
using FlexQuery.NET.Parsers.Jql;

namespace FlexQuery.Benchmarks.Benchmarks.Parsing;

public class DslParsingBenchmarks : BenchmarkBase
{
    private const string DslFilter = "status:eq:active,age:gt:25";
    private const string JqlFilter = "status = 'active' AND age > 25";
    
    [Benchmark(Baseline = true)]
    public void FlexQuery_Dsl_Parsing()
    {
        DslParser.Parse(DslFilter);
    }

    [Benchmark]
    public void FlexQuery_Jql_Parsing()
    {
        JqlParser.Parse(JqlFilter);
    }
}
