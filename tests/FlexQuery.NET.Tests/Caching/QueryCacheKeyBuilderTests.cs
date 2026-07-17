using FlexQuery.NET.Caching;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Tests.Caching;

public class QueryCacheKeyBuilderTests
{

    [Fact]
    public void Build_EmptyOptions_ReturnsKey()
    {
        var options = new QueryOptions();
        var key = QueryCacheKeyBuilder.Build(options, typeof(Customer), "query");

        key.Should().NotBeNullOrEmpty();
        key.Should().Contain("query");
        key.Should().Contain(typeof(Customer).FullName);
    }

    [Fact]
    public void Build_DifferentFilters_DifferentKeys()
    {
        var opts1 = new QueryOptions
        {
            Filter = new FilterGroup { Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "Alice" }] }
        };
        var opts2 = new QueryOptions
        {
            Filter = new FilterGroup { Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "Bob" }] }
        };

        var key1 = QueryCacheKeyBuilder.Build(opts1, typeof(Customer), "query");
        var key2 = QueryCacheKeyBuilder.Build(opts2, typeof(Customer), "query");

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void Build_SameOptions_SameKey()
    {
        var opts1 = new QueryOptions
        {
            Filter = new FilterGroup { Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "Alice" }] }
        };
        var opts2 = new QueryOptions
        {
            Filter = new FilterGroup { Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "Alice" }] }
        };

        var key1 = QueryCacheKeyBuilder.Build(opts1, typeof(Customer), "query");
        var key2 = QueryCacheKeyBuilder.Build(opts2, typeof(Customer), "query");

        key1.Should().Be(key2);
    }

    [Fact]
    public void Build_DifferentEntityTypes_DifferentKeys()
    {
        var opts = new QueryOptions();

        var key1 = QueryCacheKeyBuilder.Build(opts, typeof(Customer), "query");
        var key2 = QueryCacheKeyBuilder.Build(opts, typeof(string), "query");

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void Build_SortIncluded()
    {
        var options = new QueryOptions
        {
            Sort = [new SortNode { Field = "Name", Descending = false }]
        };

        var key = QueryCacheKeyBuilder.Build(options, typeof(Customer), "query");

        key.Should().Contain("sort=");
    }

    [Fact]
    public void Build_SelectIncluded()
    {
        var options = new QueryOptions
        {
            Select = [new SelectModel { Field = "Id" }, new SelectModel { Field = "Name" }]
        };

        var key = QueryCacheKeyBuilder.Build(options, typeof(Customer), "query");

        key.Should().Contain("select=");
    }

    [Fact]
    public void Build_IncludesIncluded()
    {
        var options = new QueryOptions
        {
            Includes = ["Orders"]
        };

        var key = QueryCacheKeyBuilder.Build(options, typeof(Customer), "query");

        key.Should().Contain("includes=");
    }

    [Fact]
    public void Build_GroupByAndAggregatesIncluded()
    {
        var options = new QueryOptions
        {
            GroupBy = ["Category"],
            Aggregates = [new AggregateModel { Function = AggregateFunction.Count, Field = "Id", Alias = "cnt" }]
        };

        var key = QueryCacheKeyBuilder.Build(options, typeof(Customer), "query");

        key.Should().Contain("groupBy=");
        key.Should().Contain("aggregates=");
    }

    [Fact]
    public void Build_DistinctIncluded()
    {
        var options = new QueryOptions { Distinct = true };

        var key = QueryCacheKeyBuilder.Build(options, typeof(Customer), "query");

        key.Should().Contain("distinct=True");
    }

    [Fact]
    public void CanCache_AlwaysReturnsTrue()
    {
        QueryCacheKeyBuilder.CanCache(new QueryOptions()).Should().BeTrue();
    }

    [Fact]
    public void Build_ExpandIncluded()
    {
        var options = new QueryOptions
        {
            Expand = [new IncludeNode { Path = "Orders" }]
        };

        var key = QueryCacheKeyBuilder.Build(options, typeof(Customer), "query");

        key.Should().Contain("filteredIncludes=");
    }
}
