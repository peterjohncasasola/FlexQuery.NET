using System.Linq.Expressions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Projection;

namespace FlexQuery.NET.Tests.Projection;

public class ProjectionEnhancerTests
{

    [Fact]
    public void ApplyCollectionWhereIfNeeded_NullFilter_ReturnsSameExpression()
    {
        var expr = Expression.Constant(new List<OrderItem>().AsQueryable());

        var result = ProjectionEnhancer.ApplyCollectionWhereIfNeeded(expr, typeof(OrderItem), null, new QueryOptions());

        result.Should().BeSameAs(expr);
    }

    [Fact]
    public void ApplyCollectionWhereIfNeeded_WithFilter_ReturnsModifiedExpression()
    {
        var expr = Expression.Constant(new List<OrderItem>().AsQueryable());
        var filter = new FilterGroup
        {
            Filters = [new FilterCondition { Field = "Sku", Operator = "eq", Value = "test" }]
        };

        var result = ProjectionEnhancer.ApplyCollectionWhereIfNeeded(expr, typeof(OrderItem), filter, new QueryOptions());

        result.Should().NotBeSameAs(expr);
    }
}
