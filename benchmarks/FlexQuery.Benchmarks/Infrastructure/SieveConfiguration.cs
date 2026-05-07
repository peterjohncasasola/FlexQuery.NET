using FlexQuery.Benchmarks.Models;
using Microsoft.Extensions.Options;
using Sieve.Models;
using Sieve.Services;

namespace FlexQuery.Benchmarks.Infrastructure;

/// <summary>
/// Sieve processor configured for benchmark entities.
///
/// Sieve uses attribute-based configuration ([Sieve] on entity properties)
/// and requires a SieveProcessor instance with IOptions&lt;SieveOptions&gt;.
///
/// For benchmarks, we use default SieveOptions (no custom filters/sorts).
/// </summary>
public class BenchmarkSieveProcessor : SieveProcessor
{
    public BenchmarkSieveProcessor(IOptions<SieveOptions> options) : base(options) { }
}

/// <summary>
/// Factory for creating SieveProcessor instances for benchmarks.
/// </summary>
public static class SieveFactory
{
    private static readonly IOptions<SieveOptions> DefaultOptions =
        Options.Create(new SieveOptions());

    public static SieveProcessor Create() => new BenchmarkSieveProcessor(DefaultOptions);

    /// <summary>
    /// Creates a SieveModel for common filter+sort+page scenarios.
    /// Sieve filter syntax: "Field==Value" for equality, "Field>Value" for gt, etc.
    /// Multiple filters: "Field1==Value1,Field2>Value2"
    /// </summary>
    public static SieveModel CreateModel(
        string? filters = null,
        string? sorts = null,
        int? page = null,
        int? pageSize = null)
    {
        return new SieveModel
        {
            Filters = filters,
            Sorts   = sorts,
            Page    = page,
            PageSize = pageSize
        };
    }
}
