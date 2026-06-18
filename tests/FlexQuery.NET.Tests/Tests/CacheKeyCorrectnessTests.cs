using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Caching;
using FluentAssertions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace FlexQuery.NET.Tests.Tests;

public class CacheKeyCorrectnessTests
{
    [Fact]
    public void Filter_CacheKeys_DifferentValues_ProduceDifferentKeys()
    {
        var options1 = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Name:eq:john" } });
        var options2 = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Name:eq:jane" } });
        
        var key1 = options1.GetCacheKey(typeof(object), "test");
        var key2 = options2.GetCacheKey(typeof(object), "test");
        
        // Different values should produce different cache keys
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void Filter_CacheKeys_DifferentOperators_ProduceDifferentKeys()
    {
        var options1 = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Name:eq:john" } });
        var options2 = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Name:contains:john" } });
        
        var key1 = options1.GetCacheKey(typeof(object), "test");
        var key2 = options2.GetCacheKey(typeof(object), "test");
        
        // Different operators should produce different cache keys
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void Filter_CacheKeys_ScopedFilters_ProduceDifferentKeys()
    {
        var options1 = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Orders.any(Total:gt:0)" } });
        var options2 = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Orders.any(Total:gt:100)" } });
        
        var key1 = options1.GetCacheKey(typeof(object), "test");
        var key2 = options2.GetCacheKey(typeof(object), "test");
        
        // Different scoped filter values should produce different cache keys
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void Filter_CacheKeys_SameFilter_SameKey()
    {
        var options1 = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Name:eq:john" } });
        var options2 = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Name:eq:john" } });
        
        var key1 = options1.GetCacheKey(typeof(object), "test");
        var key2 = options2.GetCacheKey(typeof(object), "test");
        
        // Same filters should produce the same cache key
        key1.Should().Be(key2);
    }

    [Fact]
    public void Filter_CacheKeys_SelectFields_Included()
    {
        var options1 = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "select", "Id,Name" } });
        var options2 = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "select", "Id,SSN" } });
        
        var key1 = options1.GetCacheKey(typeof(object), "test");
        var key2 = options2.GetCacheKey(typeof(object), "test");
        
        // Different select fields should produce different cache keys
        key1.Should().NotBe(key2);
    }
}