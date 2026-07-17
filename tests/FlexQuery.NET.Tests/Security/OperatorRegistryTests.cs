using FlexQuery.NET.Constants;
using FlexQuery.NET.Security;
using Xunit;

namespace FlexQuery.NET.Tests.Security;

public class OperatorRegistryTests
{
    [Fact]
    public void FilterOperators_SupportedOperators_ReturnsTrue()
    {
        var supported = new[] { "eq", "neq", "gt", "gte", "lt", "lte", "contains", "startswith", "endswith",
                                "like", "in", "notin", "between", "isnull", "isnotnull", "any", "all", "count" };

        foreach (var op in supported)
        {
            FilterOperators.IsSupported(op).Should().BeTrue($"operator '{op}' should be supported");
        }
    }

    [Fact]
    public void FilterOperators_UnsupportedOperator_ReturnsFalse()
    {
        FilterOperators.IsSupported("unsupported_op").Should().BeFalse();
    }

    [Fact]
    public void FilterOperators_NullOrEmpty_ReturnsFalse()
    {
        FilterOperators.IsSupported(null!).Should().BeFalse();
        FilterOperators.IsSupported("").Should().BeFalse();
    }

    [Fact]
    public void BinaryFactories_ContainsComparisonOperators()
    {
        OperatorRegistry.BinaryFactories.Should().ContainKey("eq");
        OperatorRegistry.BinaryFactories.Should().ContainKey("neq");
        OperatorRegistry.BinaryFactories.Should().ContainKey("gt");
        OperatorRegistry.BinaryFactories.Should().ContainKey("gte");
        OperatorRegistry.BinaryFactories.Should().ContainKey("lt");
        OperatorRegistry.BinaryFactories.Should().ContainKey("lte");
    }

    [Fact]
    public void BinaryFactories_ProduceCorrectExpressions()
    {
        var left = System.Linq.Expressions.Expression.Constant(1);
        var right = System.Linq.Expressions.Expression.Constant(2);

        OperatorRegistry.BinaryFactories["eq"](left, right).Should().BeAssignableTo<System.Linq.Expressions.BinaryExpression>()
            .Which.NodeType.Should().Be(System.Linq.Expressions.ExpressionType.Equal);
        OperatorRegistry.BinaryFactories["gt"](left, right).Should().BeAssignableTo<System.Linq.Expressions.BinaryExpression>()
            .Which.NodeType.Should().Be(System.Linq.Expressions.ExpressionType.GreaterThan);
    }
}
