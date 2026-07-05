using System.Linq.Expressions;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Constants;
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

    [Fact]
    public void CanCache_WithExpressionMappings_ReturnsTrue()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Logic = LogicOperator.And,
                Filters = [new FilterCondition { Field = "Status", Operator = "eq", Value = "active" }]
            }
        };

        // Without ExpressionMappings, caching is allowed
        QueryCacheKeyBuilder.CanCache(options).Should().BeTrue();

        // With ExpressionMappings set, caching is still allowed (mappings are part of the cache key)
        var mappings = new Dictionary<string, LambdaExpression>
        {
            ["DerivedStatus"] = Expression.Lambda(Expression.Constant("active"))
        };
        options.Items[ContextKeys.ExpressionMappings] = mappings;

        QueryCacheKeyBuilder.CanCache(options).Should().BeTrue();
    }

    [Fact]
    public void ExpressionMappings_DifferentMappings_DifferentCacheKey()
    {
        var options1 = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Logic = LogicOperator.And,
                Filters = [new FilterCondition { Field = "Status", Operator = "eq", Value = "active" }]
            }
        };
        options1.Items[ContextKeys.ExpressionMappings] = new Dictionary<string, LambdaExpression>
        {
            ["Status"] = Expression.Lambda(Expression.Property(Expression.Parameter(typeof(string)), "Length"))
        };

        var options2 = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Logic = LogicOperator.And,
                Filters = [new FilterCondition { Field = "Status", Operator = "eq", Value = "active" }]
            }
        };
        options2.Items[ContextKeys.ExpressionMappings] = new Dictionary<string, LambdaExpression>
        {
            ["Status"] = Expression.Lambda(Expression.Constant("active"))
        };

        var key1 = QueryCacheKeyBuilder.Build(options1, typeof(object), "test");
        var key2 = QueryCacheKeyBuilder.Build(options2, typeof(object), "test");

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void CacheKey_IdenticalQuery_DifferentEntityType_DifferentKey()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "Name:eq:john" } });

        var key1 = options.GetCacheKey(typeof(string), "test");
        var key2 = options.GetCacheKey(typeof(int), "test");

        // Same query on different entity types must produce different cache keys
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void SortOrder_DifferentFieldOrder_DifferentKey()
    {
        var optionsA = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "sort", "Name:asc,Id:asc" } });
        var optionsB = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "sort", "Id:asc,Name:asc" } });

        var keyA = optionsA.GetCacheKey(typeof(object), "test");
        var keyB = optionsB.GetCacheKey(typeof(object), "test");

        // Sort order matters — [Name, Id] is a different query shape than [Id, Name]
        keyA.Should().NotBe(keyB);
    }


    [Fact]
    public void SelectOrder_DifferentFieldOrder_SameKey()
    {
        var optionsA = QueryOptionsParser.Parse(
            new Dictionary<string, StringValues>
            {
                { "select", "Id,Name" }
            });

        var optionsB = QueryOptionsParser.Parse(
            new Dictionary<string, StringValues>
            {
                { "select", "Name,Id" }
            });

        var keyA = optionsA.GetCacheKey(typeof(object), "test");
        var keyB = optionsB.GetCacheKey(typeof(object), "test");

        keyA.Should().Be(keyB);
    }

    [Fact]
    public void ProjectionCacheKey_DifferentFilterValues_DifferentKeys()
    {
        var options1 = new QueryOptions
        {
            Select = ["Id", "Name"],
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Status", Operator = "eq", Value = "Active" }]
            }
        };
        options1.Items[ContextKeys.EntityType] = typeof(string);

        var options2 = new QueryOptions
        {
            Select = ["Id", "Name"],
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Status", Operator = "eq", Value = "Inactive" }]
            }
        };
        options2.Items[ContextKeys.EntityType] = typeof(string);

        var key1 = QueryCacheKeyBuilder.Build(options1, typeof(string), "projection");
        var key2 = QueryCacheKeyBuilder.Build(options2, typeof(string), "projection");

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void ProjectionCacheKey_DifferentIncludeNodes_DifferentKeys()
    {
        var options1 = new QueryOptions
        {
            Select = ["Id"],
            Expand =
            [
                new IncludeNode
                {
                    Path = "Orders",
                    Filter = new FilterGroup
                    {
                        Filters = [new FilterCondition { Field = "Total", Operator = "gt", Value = "100" }]
                    }
                }
            ]
        };
        options1.Items[ContextKeys.EntityType] = typeof(string);

        var options2 = new QueryOptions
        {
            Select = ["Id"],
            Expand =
            [
                new IncludeNode
                {
                    Path = "Orders",
                    Filter = new FilterGroup
                    {
                        Filters = [new FilterCondition { Field = "Total", Operator = "gt", Value = "500" }]
                    }
                }
            ]
        };
        options2.Items[ContextKeys.EntityType] = typeof(string);

        var key1 = QueryCacheKeyBuilder.Build(options1, typeof(string), "projection");
        var key2 = QueryCacheKeyBuilder.Build(options2, typeof(string), "projection");

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void ProjectionCacheKey_IncludesSelect_Tree_AllDimensionsCovered()
    {
        var options1 = new QueryOptions
        {
            Select = ["Id"],
            Includes = ["Orders"],
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Status", Operator = "eq", Value = "Active" }]
            }
        };
        options1.Items[ContextKeys.EntityType] = typeof(string);

        var options2 = new QueryOptions
        {
            Select = ["Id", "Name"],
            Includes = ["Orders"],
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Status", Operator = "eq", Value = "Active" }]
            }
        };
        options2.Items[ContextKeys.EntityType] = typeof(string);

        var key1 = QueryCacheKeyBuilder.Build(options1, typeof(string), "projection");
        var key2 = QueryCacheKeyBuilder.Build(options2, typeof(string), "projection");

        // Different Select fields produce different cache keys even when
        // all other dimensions (Includes, Filter) are identical
        key1.Should().NotBe(key2);
    }

    [Theory]
    [InlineData("Name:eq:alice", "Name:eq:bob")]
    [InlineData("Name:eq:alice", "Name:contains:alice")]
    [InlineData("Name:eq:alice,Age:gt:25", "Name:eq:alice,Age:lt:25")]
    [InlineData("Orders.any(Total:gt:100)", "Orders.any(Total:gt:500)")]
    public void FilterCondition_EveryDimension_AffectsCacheKey(string filter1, string filter2)
    {
        var options1 = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", filter1 } });
        var options2 = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", filter2 } });

        var key1 = options1.GetCacheKey(typeof(object), "test");
        var key2 = options2.GetCacheKey(typeof(object), "test");

        key1.Should().NotBe(key2);
    }
}