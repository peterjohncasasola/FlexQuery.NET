using BenchmarkDotNet.Attributes;
using FlexQuery.Benchmarks.Abstractions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers.Dsl;
using FlexQuery.NET.Parsers.Jql;

namespace FlexQuery.Benchmarks.Benchmarks.Parsing;

public class DslParsingBenchmarks : BenchmarkBase
{
    private const string DslFilter = "status:eq:active,age:gt:25";
    private readonly JqlQueryParser _jqlParser = new();
    private const string JqlFilter = "status = 'active' AND age > 25";
    
    [Benchmark(Baseline = true)]
    public DslAstNode FlexQuery_Dsl_Parsing()
    {
        return DslAstParser.Parse(DslFilter);
    }

    [Benchmark]
    public FilterGroup FlexQuery_Jql_Parsing()
    {
        return _jqlParser.Parse(JqlFilter);
    }
}
