using System.Linq.Expressions;
using FlexQuery.NET.Security;

namespace FlexQuery.NET.Tests.Security;

public class OperatorRegistryTests
{
    [Fact]
    public void IsAllowed_SupportedOperators_ReturnsTrue()
    {
        var supported = new[] { "eq", "neq", "gt", "gte", "lt", "lte", "contains", "startswith", "endswith",
                                "like", "in", "notin", "between", "isnull", "isnotnull", "any", "all", "count" };

        foreach (var op in supported)
        {
            OperatorRegistry.IsAllowed(op).Should().BeTrue($"operator '{op}' should be allowed");
        }
    }

    [Fact]
    public void IsAllowed_UnsupportedOperator_ReturnsFalse()
    {
        OperatorRegistry.IsAllowed("unsupported_op").Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_NullOrEmpty_ReturnsFalse()
    {
        OperatorRegistry.IsAllowed(null!).Should().BeFalse();
        OperatorRegistry.IsAllowed("").Should().BeFalse();
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
        var left = Expression.Constant(1);
        var right = Expression.Constant(2);

        OperatorRegistry.BinaryFactories["eq"](left, right).Should().BeAssignableTo<BinaryExpression>()
            .Which.NodeType.Should().Be(ExpressionType.Equal);
        OperatorRegistry.BinaryFactories["gt"](left, right).Should().BeAssignableTo<BinaryExpression>()
            .Which.NodeType.Should().Be(ExpressionType.GreaterThan);
    }
}
