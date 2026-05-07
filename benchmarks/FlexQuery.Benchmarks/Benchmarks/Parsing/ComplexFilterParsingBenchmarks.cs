using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using Gridify;
using Sieve.Models;

namespace FlexQuery.Benchmarks.Benchmarks.Parsing;

/// <summary>
/// Measures parsing overhead for complex, multi-condition filter strings.
///
/// Fair comparison notes:
/// - FlexQuery.NET parses EAGERLY into a full AST during Parse().
///   This means filter validation, operator normalization, and tree construction all happen here.
/// - Gridify and Sieve parse LAZILY — they only store the string in a DTO here.
///   Real parsing happens during Apply/ApplyFiltering. This is a valid design choice that
///   trades deferred cost for lower up-front allocation.
/// - For a true full-pipeline comparison, see EndToEndQueryBenchmarks.
///
/// OData / GraphQL exclusion:
///   OData requires an EDM model + HttpContext to parse. GraphQL requires schema + document parsing.
///   These are architecturally different and not comparable at the "string → model" level.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class ComplexFilterParsingBenchmarks
{
    // ═══════════════════════════════════════════════════════════════════════
    // Scenario 1: Multi-field AND
    // ═══════════════════════════════════════════════════════════════════════

    private const string FlexQuery_MultiAnd = "status:eq:active,age:gt:18,city:eq:London";
    private const string Gridify_MultiAnd   = "status=active, age>18, city=London";
    private const string Sieve_MultiAnd     = "Status==active,Age>18,City==London";

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("MultiField")]
    public QueryOptions FlexQuery_Parse_MultiAnd()
    {
        return QueryOptionsParser.Parse(new FlexQueryParameters { Filter = FlexQuery_MultiAnd });
    }

    [Benchmark]
    [BenchmarkCategory("MultiField")]
    public GridifyQuery Gridify_Parse_MultiAnd()
    {
        return new GridifyQuery { Filter = Gridify_MultiAnd };
    }

    [Benchmark]
    [BenchmarkCategory("MultiField")]
    public SieveModel Sieve_Parse_MultiAnd()
    {
        return new SieveModel { Filters = Sieve_MultiAnd };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Scenario 2: Nested collection predicate
    // ═══════════════════════════════════════════════════════════════════════

    // FlexQuery supports nested any() natively.
    // Gridify and Sieve do NOT support nested collection predicates out of the box.
    // This scenario is FlexQuery-only — it demonstrates a unique capability, not a comparison.
    private const string FlexQuery_Nested = "orders:any:status:eq:shipped";

    [Benchmark]
    [BenchmarkCategory("Nested")]
    public QueryOptions FlexQuery_Parse_NestedAny()
    {
        return QueryOptionsParser.Parse(new FlexQueryParameters { Filter = FlexQuery_Nested });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Scenario 3: Mixed operators (contains, gte, in)
    // ═══════════════════════════════════════════════════════════════════════

    private const string FlexQuery_Mixed = "name:contains:john,age:gte:21,status:in:active|pending";
    private const string Gridify_Mixed   = "name=*john, age>=21, status=active|pending";
    private const string Sieve_Mixed     = "Name@=john,Age>=21,Status==active|Status==pending";

    [Benchmark]
    [BenchmarkCategory("MixedOps")]
    public QueryOptions FlexQuery_Parse_MixedOperators()
    {
        return QueryOptionsParser.Parse(new FlexQueryParameters { Filter = FlexQuery_Mixed });
    }

    [Benchmark]
    [BenchmarkCategory("MixedOps")]
    public GridifyQuery Gridify_Parse_MixedOperators()
    {
        return new GridifyQuery { Filter = Gridify_Mixed };
    }

    [Benchmark]
    [BenchmarkCategory("MixedOps")]
    public SieveModel Sieve_Parse_MixedOperators()
    {
        return new SieveModel { Filters = Sieve_Mixed };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Scenario 4: Full parameter set (filter + sort + page + select)
    // ═══════════════════════════════════════════════════════════════════════

    // FlexQuery full pipeline parse
    private static readonly FlexQueryParameters FlexQuery_FullParams = new()
    {
        Filter   = "status:eq:active,age:gt:25",
        Sort     = "name:asc,createdAt:desc",
        Page     = 1,
        PageSize = 100,
        Select   = "id,name,email,city"
    };

    // Gridify full pipeline construct
    private static readonly GridifyQuery Gridify_FullParams_Template = new()
    {
        Filter   = "status=active, age>25",
        OrderBy  = "name asc, createdAt desc",
        Page     = 1,
        PageSize = 100
    };

    // Sieve full pipeline construct
    private static readonly SieveModel Sieve_FullParams_Template = new()
    {
        Filters  = "Status==active,Age>25",
        Sorts    = "Name,-CreatedAt",
        Page     = 1,
        PageSize = 100
    };

    [Benchmark]
    [BenchmarkCategory("FullPipeline")]
    public QueryOptions FlexQuery_Parse_FullParameterSet()
    {
        return QueryOptionsParser.Parse(FlexQuery_FullParams);
    }

    [Benchmark]
    [BenchmarkCategory("FullPipeline")]
    public GridifyQuery Gridify_Parse_FullParameterSet()
    {
        // Gridify defers all parsing — this only copies the strings.
        return new GridifyQuery
        {
            Filter   = Gridify_FullParams_Template.Filter,
            OrderBy  = Gridify_FullParams_Template.OrderBy,
            Page     = Gridify_FullParams_Template.Page,
            PageSize = Gridify_FullParams_Template.PageSize
        };
    }

    [Benchmark]
    [BenchmarkCategory("FullPipeline")]
    public SieveModel Sieve_Parse_FullParameterSet()
    {
        // Sieve defers all parsing — this only copies the strings.
        return new SieveModel
        {
            Filters  = Sieve_FullParams_Template.Filters,
            Sorts    = Sieve_FullParams_Template.Sorts,
            Page     = Sieve_FullParams_Template.Page,
            PageSize = Sieve_FullParams_Template.PageSize
        };
    }
}
