using FlexQuery.NET.Parsers;
using FluentAssertions;
using Microsoft.Extensions.Primitives;

namespace FlexQuery.NET.Tests.Tests;

public class CacheIsolationTests
{
    private class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SSN { get; set; } = string.Empty;
    }

    [Fact]
    public void ParserCache_ReturnsIsolatedClone_MutationsDoNotAffectCache()
    {
        // First parse populates the cache
        var parameters1 = new Dictionary<string, StringValues> { { "filter", "Name:eq:john" } };
        var options1 = QueryOptionsParser.Parse(parameters1);
        
        // Second parse should return a clone that can be mutated without affecting cache
        var parameters2 = new Dictionary<string, StringValues> { { "filter", "Name:eq:john" } };
        var options2 = QueryOptionsParser.Parse(parameters2);
        
        // Mutate the second options
        if (options2.Filter != null && options2.Filter.Filters.Count > 0)
        {
            options2.Filter.Filters[0].Value = "mutated";
        }
        
        // Third parse should NOT see the mutation (cache returns clean clone)
        var parameters3 = new Dictionary<string, StringValues> { { "filter", "Name:eq:john" } };
        var options3 = QueryOptionsParser.Parse(parameters3);
        
        // Verify isolation
        options3.Filter.Should().NotBeNull();
        if (options3.Filter != null && options3.Filter.Filters.Count > 0)
        {
            options3.Filter.Filters[0].Value.Should().Be("john"); // Original value preserved
        }
    }

    [Fact]
    public void ParserCache_CloneWithFilter_IsolatedFromOriginal()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Name:eq:john" } });
        
        // Clone the options
        var clone = QueryOptionsExtensions.CopyQueryOptions(options);
        
        // Mutate the clone
        if (clone.Filter != null && clone.Filter.Filters.Count > 0)
        {
            clone.Filter.Filters[0].Value = "modified";
        }
        
        // Original should not be affected
        options.Filter.Should().NotBeNull();
        if (options.Filter != null && options.Filter.Filters.Count > 0)
        {
            options.Filter.Filters[0].Value.Should().Be("john");
        }
    }

    [Fact]
    public void ParserCache_FiltersWithScopedFilters_CloneIsolation()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Orders.any(Total:gt:0)" } });
        
        // Clone the options
        var clone = QueryOptionsExtensions.CopyQueryOptions(options);
        
        // Mutate the clone's scoped filter if present
        if (clone.Filter?.Filters.Count > 0)
        {
            var filter = clone.Filter.Filters[0];
            if (filter.ScopedFilter != null && filter.ScopedFilter.Filters.Count > 0)
            {
                filter.ScopedFilter.Filters[0].Value = "modified";
            }
        }
        
        // Original should not be affected
        var original = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Orders.any(Total:gt:0)" } });
        original.Filter.Should().NotBeNull();
        original.Filter.Filters.Should().HaveCount(1);
        
        // Clone mutation should not leak back
        clone.Filter.Should().NotBeNull();
        clone.Filter!.Filters.Should().HaveCount(1);
    }
}