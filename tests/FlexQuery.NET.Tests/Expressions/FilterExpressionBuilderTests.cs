using System.Linq.Expressions;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Expressions;
using Xunit;

namespace FlexQuery.NET.Tests.Expressions;

public class FilterExpressionBuilderTests
{
    private class NullableModel
    {
        public int? Score { get; set; }
    }

    private static Func<T, bool> Compile<T>(Expression member, string op, string? value)
    {
        var expr = FilterExpressionBuilder.Build(member, op, value)
            ?? throw new InvalidOperationException($"Build returned null for op={op} value={value}");
        var param = (ParameterExpression)((MemberExpression)member).Expression!;
        return Expression.Lambda<Func<T, bool>>(expr, param).Compile();
    }

    [Theory]
    [InlineData("contains", "oh", "John", true)]
    [InlineData("contains", "xy", "John", false)]
    [InlineData("startswith", "Jo", "John", true)]
    [InlineData("startswith", "oh", "John", false)]
    [InlineData("endswith", "hn", "John", true)]
    [InlineData("endswith", "Jo", "John", false)]
    public void StringMethods_MatchCorrectly(string op, string value, string name, bool expected)
    {
        var param = Expression.Parameter(typeof(Customer));
        var member = Expression.Property(param, nameof(Customer.Name));
        var predicate = Compile<Customer>(member, op, value);

        predicate(new Customer { Name = name }).Should().Be(expected);
    }

    [Fact]
    public void Contains_WithNullValue_UsesEmptyString()
    {
        var param = Expression.Parameter(typeof(Customer));
        var member = Expression.Property(param, nameof(Customer.Name));
        var expr = FilterExpressionBuilder.Build(member, FilterOperators.Contains, null);

        expr.Should().NotBeNull();
        expr!.NodeType.Should().Be(ExpressionType.Call);
    }

    [Theory]
    [InlineData("eq", "John", "John", true)]
    [InlineData("eq", "John", "Jane", false)]
    [InlineData("neq", "John", "Jane", true)]
    [InlineData("neq", "John", "John", false)]
    public void StringEqual_ComparesDirectly(string op, string value, string name, bool expected)
    {
        var param = Expression.Parameter(typeof(Customer));
        var member = Expression.Property(param, nameof(Customer.Name));
        var predicate = Compile<Customer>(member, op, value);

        predicate(new Customer { Name = name }).Should().Be(expected);
    }

    [Theory]
    [InlineData("eq", "30", 30, true)]
    [InlineData("eq", "30", 31, false)]
    [InlineData("gt", "25", 30, true)]
    [InlineData("gt", "25", 20, false)]
    [InlineData("gte", "30", 30, true)]
    [InlineData("lt", "30", 20, true)]
    [InlineData("lte", "30", 30, true)]
    public void NumericComparison_Evaluates(string op, string value, int age, bool expected)
    {
        var param = Expression.Parameter(typeof(Customer));
        var member = Expression.Property(param, nameof(Customer.Age));
        var predicate = Compile<Customer>(member, op, value);

        predicate(new Customer { Age = age }).Should().Be(expected);
    }

    [Fact]
    public void In_WithCommaSeparatedValues_MatchesAny()
    {
        var param = Expression.Parameter(typeof(Customer));
        var member = Expression.Property(param, nameof(Customer.Age));
        var predicate = Compile<Customer>(member, FilterOperators.In, "20,30,40");

        predicate(new Customer { Age = 30 }).Should().BeTrue();
        predicate(new Customer { Age = 25 }).Should().BeFalse();
    }

    [Fact]
    public void In_WithPartiallyUnconvertibleValues_SkipsBadValues()
    {
        var param = Expression.Parameter(typeof(Customer));
        var member = Expression.Property(param, nameof(Customer.Age));
        var predicate = Compile<Customer>(member, FilterOperators.In, "20,abc,30");

        predicate(new Customer { Age = 20 }).Should().BeTrue();
        predicate(new Customer { Age = 30 }).Should().BeTrue();
        predicate(new Customer { Age = 25 }).Should().BeFalse();
    }

    [Fact]
    public void In_WithEmptyValue_ReturnsConstantFalse()
    {
        var param = Expression.Parameter(typeof(Customer));
        var member = Expression.Property(param, nameof(Customer.Age));
        var expr = FilterExpressionBuilder.Build(member, FilterOperators.In, "   ");

        expr.Should().BeOfType<ConstantExpression>()
            .Which.Value.Should().Be(false);
    }

    [Fact]
    public void NotIn_NegatesInResult()
    {
        var param = Expression.Parameter(typeof(Customer));
        var member = Expression.Property(param, nameof(Customer.Age));
        var predicate = Compile<Customer>(member, FilterOperators.NotIn, "20,30,40");

        predicate(new Customer { Age = 25 }).Should().BeTrue();
        predicate(new Customer { Age = 30 }).Should().BeFalse();
    }

    [Fact]
    public void Between_WithinBounds_Matches()
    {
        var param = Expression.Parameter(typeof(Customer));
        var member = Expression.Property(param, nameof(Customer.Age));
        var predicate = Compile<Customer>(member, FilterOperators.Between, "20,40");

        predicate(new Customer { Age = 30 }).Should().BeTrue();
        predicate(new Customer { Age = 50 }).Should().BeFalse();
    }

    [Fact]
    public void Between_WithSingleBound_ReturnsNull()
    {
        var param = Expression.Parameter(typeof(Customer));
        var member = Expression.Property(param, nameof(Customer.Age));

        FilterExpressionBuilder.Build(member, FilterOperators.Between, "20").Should().BeNull();
    }

    [Fact]
    public void Between_OnNonComparableType_ReturnsNull()
    {
        var param = Expression.Parameter(typeof(Customer));
        var member = Expression.Property(param, nameof(Customer.Name));

        FilterExpressionBuilder.Build(member, FilterOperators.Between, "a,z").Should().BeNull();
    }

    [Fact]
    public void IsNull_OnNonNullableValueType_ReturnsConstantFalse()
    {
        var param = Expression.Parameter(typeof(Customer));
        var member = Expression.Property(param, nameof(Customer.Age));
        var expr = FilterExpressionBuilder.Build(member, FilterOperators.IsNull, null);

        expr.Should().BeOfType<ConstantExpression>()
            .Which.Value.Should().Be(false);
    }

    [Fact]
    public void IsNotNull_OnNonNullableValueType_ReturnsConstantTrue()
    {
        var param = Expression.Parameter(typeof(Customer));
        var member = Expression.Property(param, nameof(Customer.Age));
        var expr = FilterExpressionBuilder.Build(member, FilterOperators.IsNotNull, null);

        expr.Should().BeOfType<ConstantExpression>()
            .Which.Value.Should().Be(true);
    }

    [Fact]
    public void IsNull_OnNullableValueType_EvaluatesNullCheck()
    {
        var param = Expression.Parameter(typeof(NullableModel));
        var member = Expression.Property(param, nameof(NullableModel.Score));
        var predicate = Compile<NullableModel>(member, FilterOperators.IsNull, null);

        predicate(new NullableModel { Score = null }).Should().BeTrue();
        predicate(new NullableModel { Score = 5 }).Should().BeFalse();
    }

    [Fact]
    public void IsNotNull_OnNullableValueType_EvaluatesNullCheck()
    {
        var param = Expression.Parameter(typeof(NullableModel));
        var member = Expression.Property(param, nameof(NullableModel.Score));
        var predicate = Compile<NullableModel>(member, FilterOperators.IsNotNull, null);

        predicate(new NullableModel { Score = 5 }).Should().BeTrue();
        predicate(new NullableModel { Score = null }).Should().BeFalse();
    }

    [Fact]
    public void Comparison_OnNonComparableType_ReturnsNull()
    {
        var param = Expression.Parameter(typeof(Customer));
        var member = Expression.Property(param, nameof(Customer.Name));

        FilterExpressionBuilder.Build(member, FilterOperators.GreaterThan, "x").Should().BeNull();
    }

    [Fact]
    public void UnknownOperator_ReturnsNull()
    {
        var param = Expression.Parameter(typeof(Customer));
        var member = Expression.Property(param, nameof(Customer.Age));

        FilterExpressionBuilder.Build(member, "bogus", "1").Should().BeNull();
    }
}
