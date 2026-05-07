using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using FlexQuery.Benchmarks.Infrastructure;
using FlexQuery.Benchmarks.Infrastructure.Database;
using FlexQuery.Benchmarks.Infrastructure.Seed;
using FlexQuery.Benchmarks.Models;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using Gridify;
using Sieve.Models;
using Sieve.Services;
using System.Linq.Dynamic.Core;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.Benchmarks.Benchmarks.Execution;

/// <summary>
/// End-to-end query execution benchmarks using EF Core InMemory provider.
///
/// These benchmarks measure the FULL pipeline:
///   parse → expression generation → LINQ composition → materialization
///
/// Important caveats:
/// - InMemory provider does NOT test SQL translation. For SQL benchmarks, swap provider.
/// - Gridify applies filter + sort + paging via chained extension methods.
/// - Sieve applies all operations through SieveProcessor.Apply().
/// - Dynamic.Core uses string-based Where/OrderBy.
/// - Handwritten LINQ is the theoretical optimum — the baseline.
///
/// OData: Requires full ASP.NET Core pipeline (ODataQueryOptions, EdmModel, HttpContext).
///   A separate OData benchmark class with proper middleware setup is recommended.
///
/// GraphQL (Hot Chocolate): Requires schema registration, query execution engine,
///   and middleware pipeline. It solves a fundamentally different problem (graph traversal
///   with client-defined shapes). Direct comparison at this level would be misleading.
///
/// Dataset: 1,000 users, ~2,500 orders, ~5,000 items (deterministic seed).
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class EndToEndQueryBenchmarks
{
    private BenchmarkDbContext _db = null!;
    private IQueryable<User> _users = null!;

    // Pre-parsed options (parsing cost is measured separately in Parsing benchmarks)
    private QueryOptions _flexFilterSortPage = null!;
    private QueryOptions _flexNestedAny = null!;
    private QueryOptions _flexProjection = null!;

    private GridifyQuery _gridifyFilterSortPage = null!;
    private SieveProcessor _sieveProcessor = null!;
    private SieveModel _sieveFilterSortPage = null!;
    private string _dynamicLinqFilter = null!;

    [GlobalSetup]
    public void Setup()
    {
        _db = BenchmarkDbContextFactory.Create();
        _users = _db.Users.AsNoTracking().AsQueryable();

        // ── FlexQuery pre-parsed options ─────────────────────────────────
        _flexFilterSortPage = QueryOptionsParser.Parse(new FlexQueryParameters
        {
            Filter   = "status:eq:active,age:gt:25",
            Sort     = "name:asc",
            Page     = 1,
            PageSize = 100
        });

        _flexNestedAny = QueryOptionsParser.Parse(new FlexQueryParameters
        {
            Filter = "orders:any:status:eq:completed"
        });

        _flexProjection = QueryOptionsParser.Parse(new FlexQueryParameters
        {
            Filter = "status:eq:active",
            Select = "id,name,email,city"
        });

        // ── Gridify ──────────────────────────────────────────────────────
        _gridifyFilterSortPage = new GridifyQuery
        {
            Filter   = "status=active, age>25",
            OrderBy  = "name asc",
            Page     = 1,
            PageSize = 100
        };

        // ── Sieve ────────────────────────────────────────────────────────
        _sieveProcessor = SieveFactory.Create();
        _sieveFilterSortPage = new SieveModel
        {
            Filters  = "Status==active,Age>25",
            Sorts    = "Name",
            Page     = 1,
            PageSize = 100
        };

        // ── Dynamic LINQ ─────────────────────────────────────────────────
        _dynamicLinqFilter = "Status == \"active\" && Age > 25";
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Scenario 1: Filter + Sort + Paging
    // The most common API query pattern. Every library supports this.
    // ═══════════════════════════════════════════════════════════════════════

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("FilterSortPage")]
    public List<User> Handwritten_FilterSortPage()
    {
        return _users
            .Where(u => u.Status == "active" && u.Age > 25)
            .OrderBy(u => u.Name)
            .Skip(0).Take(100)
            .ToList();
    }

    [Benchmark]
    [BenchmarkCategory("FilterSortPage")]
    public List<User> FlexQuery_FilterSortPage()
    {
        return QueryBuilder.Apply(_users, _flexFilterSortPage).ToList();
    }

    [Benchmark]
    [BenchmarkCategory("FilterSortPage")]
    public List<User> Gridify_FilterSortPage()
    {
        return _users
            .ApplyFiltering(_gridifyFilterSortPage)
            .ApplyOrdering(_gridifyFilterSortPage)
            .ApplyPaging(_gridifyFilterSortPage)
            .ToList();
    }

    [Benchmark]
    [BenchmarkCategory("FilterSortPage")]
    public List<User> Sieve_FilterSortPage()
    {
        return _sieveProcessor.Apply(_sieveFilterSortPage, _users).ToList();
    }

    [Benchmark]
    [BenchmarkCategory("FilterSortPage")]
    public List<User> DynamicLinq_FilterSortPage()
    {
        return _users
            .Where(_dynamicLinqFilter)
            .OrderBy("Name asc")
            .Skip(0).Take(100)
            .ToList();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Scenario 2: Nested collection predicate (any)
    // FlexQuery + Handwritten support this natively.
    // Gridify/Sieve do NOT support nested any() — this is a feature gap, not a flaw.
    // Dynamic.Core supports it but with significantly different syntax.
    // ═══════════════════════════════════════════════════════════════════════

    [Benchmark]
    [BenchmarkCategory("NestedAny")]
    public List<User> Handwritten_NestedAny()
    {
        return _users
            .Where(u => u.Orders.Any(o => o.Status == "completed"))
            .ToList();
    }

    [Benchmark]
    [BenchmarkCategory("NestedAny")]
    public List<User> FlexQuery_NestedAny()
    {
        return QueryBuilder.ApplyFilter(_users, _flexNestedAny).ToList();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Scenario 3: Projection (select subset of fields)
    // Gridify/Sieve do not support server-side projection out of the box.
    // Dynamic.Core supports it via .Select("new(Field1, Field2)").
    // ═══════════════════════════════════════════════════════════════════════

    [Benchmark]
    [BenchmarkCategory("Projection")]
    public List<object> Handwritten_Projection()
    {
        return _users
            .Where(u => u.Status == "active")
            .Select(u => (object)new { u.Id, u.Name, u.Email, u.City })
            .ToList();
    }

    [Benchmark]
    [BenchmarkCategory("Projection")]
    public List<object> FlexQuery_Projection()
    {
        var filtered = QueryBuilder.ApplyFilter(_users, _flexProjection);
        return QueryBuilder.ApplySelect(filtered, _flexProjection).ToList();
    }

    [Benchmark]
    [BenchmarkCategory("Projection")]
    public List<object> DynamicLinq_Projection()
    {
        return _users
            .Where("Status == \"active\"")
            .Select("new(Id, Name, Email, City)")
            .ToDynamicList<object>();
    }
}
