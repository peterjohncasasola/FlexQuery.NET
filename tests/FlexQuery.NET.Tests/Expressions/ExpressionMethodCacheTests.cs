using System.Reflection;
using FlexQuery.NET.Expressions;

namespace FlexQuery.NET.Tests.Expressions;

public class ExpressionMethodCacheTests
{

    [Fact]
    public void QueryableGroupBy_ReturnsMethodInfo()
    {
        var method = ExpressionMethodCache.QueryableGroupBy();
        method.Should().NotBeNull();
        method.Name.Should().Be(nameof(Queryable.GroupBy));
    }

    [Fact]
    public void QueryableSelect_ReturnsMethodInfo()
    {
        var method = ExpressionMethodCache.QueryableSelect();
        method.Should().NotBeNull();
        method.Name.Should().Be(nameof(Queryable.Select));
    }

    [Fact]
    public void QueryableWhere_ReturnsMethodInfo()
    {
        var method = ExpressionMethodCache.QueryableWhere();
        method.Should().NotBeNull();
        method.Name.Should().Be(nameof(Queryable.Where));
    }

    [Fact]
    public void QueryableOrderBy_ReturnsMethodInfo()
    {
        var method = ExpressionMethodCache.QueryableOrderBy();
        method.Should().NotBeNull();
        method.Name.Should().Be(nameof(Queryable.OrderBy));
    }

    [Fact]
    public void QueryableOrderByDescending_ReturnsMethodInfo()
    {
        var method = ExpressionMethodCache.QueryableOrderByDescending();
        method.Should().NotBeNull();
        method.Name.Should().Be(nameof(Queryable.OrderByDescending));
    }

    [Fact]
    public void QueryableThenBy_ReturnsMethodInfo()
    {
        var method = ExpressionMethodCache.QueryableThenBy();
        method.Should().NotBeNull();
        method.Name.Should().Be(nameof(Queryable.ThenBy));
    }

    [Fact]
    public void QueryableThenByDescending_ReturnsMethodInfo()
    {
        var method = ExpressionMethodCache.QueryableThenByDescending();
        method.Should().NotBeNull();
        method.Name.Should().Be(nameof(Queryable.ThenByDescending));
    }

    [Fact]
    public void QueryableSelectMany_ReturnsMethodInfo()
    {
        var method = ExpressionMethodCache.QueryableSelectMany();
        method.Should().NotBeNull();
        method.Name.Should().Be(nameof(Queryable.SelectMany));
    }

    [Fact]
    public void EnumerableAnyWithPredicate_BindsCorrectType()
    {
        var method = ExpressionMethodCache.EnumerableAnyWithPredicate(typeof(Product));

        method.Should().NotBeNull();
        method.Name.Should().Be(nameof(Enumerable.Any));
        method.GetGenericArguments().Should().ContainSingle().Which.Should().Be(typeof(Product));
    }

    [Fact]
    public void EnumerableAll_BindsCorrectType()
    {
        var method = ExpressionMethodCache.EnumerableAll(typeof(Product));

        method.Should().NotBeNull();
        method.Name.Should().Be(nameof(Enumerable.All));
        method.GetGenericArguments().Should().ContainSingle().Which.Should().Be(typeof(Product));
    }

    [Fact]
    public void EnumerableCount_BindsCorrectType()
    {
        var method = ExpressionMethodCache.EnumerableCount(typeof(Product));

        method.Should().NotBeNull();
        method.Name.Should().Be(nameof(Enumerable.Count));
        method.GetGenericArguments().Should().ContainSingle().Which.Should().Be(typeof(Product));
    }

    [Fact]
    public void EnumerableToList_ReturnsOpenGeneric()
    {
        var method = ExpressionMethodCache.EnumerableToList();

        method.Should().NotBeNull();
        method.Name.Should().Be(nameof(Enumerable.ToList));
        method.IsGenericMethodDefinition.Should().BeTrue();
    }

    [Fact]
    public void EnumerableMinWithSelector_BindsCorrectTypes()
    {
        var method = ExpressionMethodCache.EnumerableMinWithSelector(typeof(Product), typeof(int));

        method.Should().NotBeNull();
        method.Name.Should().Be(nameof(Enumerable.Min));
    }

    [Fact]
    public void EnumerableMaxWithSelector_BindsCorrectTypes()
    {
        var method = ExpressionMethodCache.EnumerableMaxWithSelector(typeof(Product), typeof(string));

        method.Should().NotBeNull();
        method.Name.Should().Be(nameof(Enumerable.Max));
    }

    [Fact]
    public void EnumerableSumWithSelector_BindsCorrectTypes()
    {
        var method = ExpressionMethodCache.EnumerableSumWithSelector(typeof(Product), typeof(decimal));

        method.Should().NotBeNull();
        method.Name.Should().Be(nameof(Enumerable.Sum));
    }

    [Fact]
    public void EnumerableAverageWithSelector_BindsCorrectTypes()
    {
        var method = ExpressionMethodCache.EnumerableAverageWithSelector(typeof(Product), typeof(decimal));

        method.Should().NotBeNull();
        method.Name.Should().Be(nameof(Enumerable.Average));
    }

    [Fact]
    public void QueryableWhereSimple_ReturnsMethodInfo()
    {
        var method = ExpressionMethodCache.QueryableWhereSimple();

        method.Should().NotBeNull();
        method.Name.Should().Be(nameof(Queryable.Where));
    }

    [Fact]
    public void QueryableSelectSimple_ReturnsMethodInfo()
    {
        var method = ExpressionMethodCache.QueryableSelectSimple();

        method.Should().NotBeNull();
        method.Name.Should().Be(nameof(Queryable.Select));
    }

    [Fact]
    public void QueryableAsQueryable_ReturnsMethodInfo()
    {
        var method = ExpressionMethodCache.QueryableAsQueryable();
        method.Should().NotBeNull();
        method.Name.Should().Be(nameof(Queryable.AsQueryable));
    }

    [Fact]
    public void CachedMethods_AreStable()
    {
        var method1 = ExpressionMethodCache.QueryableGroupBy();
        var method2 = ExpressionMethodCache.QueryableGroupBy();

        method1.Should().BeSameAs(method2);
    }

    [Fact]
    public void EnumerableAnyWithPredicate_CachesPerType()
    {
        var method1 = ExpressionMethodCache.EnumerableAnyWithPredicate(typeof(Product));
        var method2 = ExpressionMethodCache.EnumerableAnyWithPredicate(typeof(Product));

        method1.Should().BeSameAs(method2);
    }

    [Fact]
    public void EnumerableAnyWithPredicate_DifferentTypes_DifferentMethods()
    {
        var method1 = ExpressionMethodCache.EnumerableAnyWithPredicate(typeof(Product));
        var method2 = ExpressionMethodCache.EnumerableAnyWithPredicate(typeof(string));

        method1.Should().NotBeSameAs(method2);
    }
}
