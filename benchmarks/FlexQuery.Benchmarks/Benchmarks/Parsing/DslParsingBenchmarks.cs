using BenchmarkDotNet.Attributes;
using FlexQuery.Benchmarks.Abstractions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers.Dsl;
using FlexQuery.NET.Parsers.Fql;

namespace FlexQuery.Benchmarks.Benchmarks.Parsing;

public class DslParsingBenchmarks : BenchmarkBase
{
    private const string DslFilter = "status:eq:active,age:gt:25";
    private readonly FqlQueryParser _fqlParser = new();
    private const string FqlFilter = "status = 'active' AND age > 25";
    
    [Benchmark]
    public FilterGroup FlexQuery_Fql_Parsing()
    {
        return _fqlParser.Parse(FqlFilter);
    }

    [Benchmark]
    public FilterGroup FlexQuery_Jql_Parsing()
    {
        return _jqlParser.Parse(JqlFilter);
    }
}
