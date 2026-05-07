using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using FlexQuery.Benchmarks.Infrastructure;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using Gridify;
using Sieve.Models;
using Sieve.Services;

namespace FlexQuery.Benchmarks.Benchmarks.Parsing;

/// <summary>
/// Measures the cost of constructing a query model from a simple equality filter string.
///
/// Fair comparison notes:
/// - FlexQuery.NET: Eagerly parses into a full AST (FilterGroupNode tree) during Parse().
/// - Gridify: Deferred — only constructs a GridifyQuery DTO; actual parsing happens at Apply time.
/// - Sieve: Deferred — only constructs a SieveModel DTO; actual parsing happens at Apply time.
///
/// This means FlexQuery does MORE work here (full parse) while Gridify/Sieve do LESS (DTO construction).
/// For a true apples-to-apples comparison, see the EndToEndQueryBenchmarks.
///
/// OData note: OData parsing requires a full EDM model + HttpContext pipeline.
///   It is not comparable at the "string parsing" level — it operates at a different abstraction.
///
/// GraphQL note: GraphQL parsing is a fundamentally different operation (schema + document parsing).
///   Comparing it here would be misleading. See the benchmark README for architectural analysis.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SimpleFilterParsingBenchmarks
{
    // ── Filter strings per library syntax ────────────────────────────────

    // FlexQuery DSL: "Status:eq:Active"
    private const string FlexQueryDsl = "Status:eq:Active";

    // Gridify: "Status==Active"
    private const string GridifyDsl = "Status==Active";

    // Sieve: "Status==Active"
    private const string SieveDsl = "Status==Active";

    // Dynamic.Core: "Status == \"Active\""  (parsed at .Where() time, not here)
    private const string DynamicLinqExpr = "Status == \"Active\"";

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Simple")]
    public QueryOptions FlexQuery_Parse()
    {
        return QueryOptionsParser.Parse(new FlexQueryParameters { Filter = FlexQueryDsl });
    }

    [Benchmark]
    [BenchmarkCategory("Simple")]
    public GridifyQuery Gridify_Parse()
    {
        // Note: Gridify defers parsing — this only constructs the DTO.
        return new GridifyQuery { Filter = GridifyDsl };
    }

    [Benchmark]
    [BenchmarkCategory("Simple")]
    public SieveModel Sieve_Parse()
    {
        // Note: Sieve defers parsing — this only constructs the DTO.
        return new SieveModel { Filters = SieveDsl };
    }
}
