using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Projection;

namespace FlexQuery.NET.Tests.Projection;

public class ProjectionEnhancerTests
{
    private sealed class TestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void ApplyCollectionWhereIfNeeded_NullFilter_ReturnsSameExpression()
    {
        var expr = System.Linq.Expressions.Expression.Constant(new List<TestItem>().AsQueryable());

        var result = ProjectionEnhancer.ApplyCollectionWhereIfNeeded(expr, typeof(TestItem), null, new QueryOptions());

        result.Should().BeSameAs(expr);
    }

    [Fact]
    public void ApplyCollectionWhereIfNeeded_WithFilter_ReturnsModifiedExpression()
    {
        var expr = System.Linq.Expressions.Expression.Constant(new List<TestItem>().AsQueryable());
        var filter = new FilterGroup
        {
            Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "test" }]
        };

        var result = ProjectionEnhancer.ApplyCollectionWhereIfNeeded(expr, typeof(TestItem), filter, new QueryOptions());

        result.Should().NotBeSameAs(expr);
    }
}
