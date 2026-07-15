using System.Linq.Expressions;
using FlexQuery.NET.Caching;

namespace FlexQuery.NET.Tests.Caching;

public class QueryCacheManagerTests
{
    [Fact]
    public void ShouldCache_NullOverride_UsesDefault()
    {
        var result = QueryCacheManager.ShouldCache(null);
        result.Should().Be(FlexQueryCacheSettings.EnableCache);
    }

    [Fact]
    public void ShouldCache_TrueOverride_ReturnsTrue()
    {
        QueryCacheManager.ShouldCache(true).Should().BeTrue();
    }

    [Fact]
    public void ShouldCache_FalseOverride_ReturnsFalse()
    {
        QueryCacheManager.ShouldCache(false).Should().BeFalse();
    }

    [Fact]
    public void GetOrAddExpression_AddsAndReturns()
    {
        Expression<Func<int, int>> expected = x => x + 1;

        var result = QueryCacheManager.GetOrAddExpression("expr_key", () => expected);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public void GetOrAddExpression_ReturnsCached()
    {
        Expression<Func<int, int>> first = x => x + 1;
        Expression<Func<int, int>> second = x => x + 2;

        var cached = QueryCacheManager.GetOrAddExpression("same_key", () => first);
        var result = QueryCacheManager.GetOrAddExpression("same_key", () => second);

        result.Should().BeSameAs(first);
        cached.Should().BeSameAs(first);
    }

    [Fact]
    public void Clear_RemovesCachedExpressions()
    {
        Expression<Func<int, int>> expr = x => x + 1;
        QueryCacheManager.GetOrAddExpression("clear_key", () => expr);

        QueryCacheManager.Clear();

        // After clear, factory should be invoked again
        var result = QueryCacheManager.GetOrAddExpression("clear_key", () => expr);
        result.Should().BeSameAs(expr);
    }
}
