using FlexQuery.NET.Filters;
using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Tests.Filters;

public class FilterAnalyzerTests
{
    [Fact]
    public void ExtractForNavigation_EmptyGroup_ReturnsNull()
    {
        var group = new FilterGroupNode { Logic = LogicOperator.And };

        var result = FilterAnalyzer.ExtractForNavigation(group, "Orders");

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractForNavigation_NullNavigation_ReturnsNull()
    {
        var group = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            Children = [new FilterConditionNode { Field = "Orders.Total", Operator = "gt", Value = "100" }]
        };

        var result = FilterAnalyzer.ExtractForNavigation(group, "");

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractForNavigation_AndGroup_ExtractsMatchingConditions()
    {
        var group = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            Children =
            [
                new FilterConditionNode { Field = "Orders.Total", Operator = "gt", Value = "100" },
                new FilterConditionNode { Field = "Name", Operator = "eq", Value = "Alice" },
                new FilterConditionNode { Field = "Orders.Status", Operator = "eq", Value = "Shipped" }
            ]
        };

        var result = FilterAnalyzer.ExtractForNavigation(group, "Orders");

        result.Should().NotBeNull();
        result!.Logic.Should().Be(LogicOperator.And);
        result.Children.Should().HaveCount(2);
        result.Children.OfType<FilterConditionNode>().Should().Contain(c => c.Field == "Total" && c.Value == "100");
        result.Children.OfType<FilterConditionNode>().Should().Contain(c => c.Field == "Status" && c.Value == "Shipped");
    }

    [Fact]
    public void ExtractForNavigation_OrGroup_WhenAllMatch_ReturnsExtracted()
    {
        var group = new FilterGroupNode
        {
            Logic = LogicOperator.Or,
            Children =
            [
                new FilterConditionNode { Field = "Orders.Total", Operator = "gt", Value = "100" },
                new FilterConditionNode { Field = "Orders.Total", Operator = "lt", Value = "200" }
            ]
        };

        var result = FilterAnalyzer.ExtractForNavigation(group, "Orders");

        result.Should().NotBeNull();
        result!.Children.Should().HaveCount(2);
    }

    [Fact]
    public void ExtractForNavigation_OrGroup_WhenNotAllMatch_ReturnsNull()
    {
        var group = new FilterGroupNode
        {
            Logic = LogicOperator.Or,
            Children =
            [
                new FilterConditionNode { Field = "Orders.Total", Operator = "gt", Value = "100" },
                new FilterConditionNode { Field = "Name", Operator = "eq", Value = "Alice" }
            ]
        };

        var result = FilterAnalyzer.ExtractForNavigation(group, "Orders");

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractForNavigation_NoMatchingConditions_ReturnsNull()
    {
        var group = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            Children = [new FilterConditionNode { Field = "Name", Operator = "eq", Value = "Alice" }]
        };

        var result = FilterAnalyzer.ExtractForNavigation(group, "Orders");

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractForNavigation_StripsNavigationPrefix()
    {
        var group = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            Children = [new FilterConditionNode { Field = "Orders.Total", Operator = "gt", Value = "100" }]
        };

        var result = FilterAnalyzer.ExtractForNavigation(group, "Orders");

        result!.Children.Should().ContainSingle()
            .Which.Should().BeOfType<FilterConditionNode>()
            .Which.Field.Should().Be("Total");
    }

    [Fact]
    public void ExtractForNavigation_NestedGroup_ExtractsRecursively()
    {
        var group = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            Children =
            [
                new FilterConditionNode { Field = "Name", Operator = "eq", Value = "Alice" },
                new FilterGroupNode
                {
                    Logic = LogicOperator.And,
                    Children =
                    [
                        new FilterConditionNode { Field = "Orders.Total", Operator = "gt", Value = "100" }
                    ]
                }
            ]
        };

        var result = FilterAnalyzer.ExtractForNavigation(group, "Orders");

        result.Should().NotBeNull();
        result!.Children.Should().ContainSingle()
            .Which.Should().BeOfType<FilterGroupNode>()
            .Which.Children.Should().ContainSingle()
            .Which.Should().BeOfType<FilterConditionNode>()
            .Which.Field.Should().Be("Total");
    }

    [Fact]
    public void CacheKey_Null_ReturnsEmpty()
    {
        FilterAnalyzer.CacheKey(null).Should().BeEmpty();
    }

    [Fact]
    public void CacheKey_Deterministic()
    {
        var group = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            Children = [new FilterConditionNode { Field = "Name", Operator = "eq", Value = "Alice" }]
        };

        var key1 = FilterAnalyzer.CacheKey(group);
        var key2 = FilterAnalyzer.CacheKey(group);

        key1.Should().Be(key2);
    }

    [Fact]
    public void ExtractForNavigation_ExactNavigationMatch_RebasesCorrectly()
    {
        var group = new FilterGroupNode
        {
            Logic = LogicOperator.And,
            Children = [new FilterConditionNode { Field = "Orders.Total", Operator = "gt", Value = "100" }]
        };

        var result = FilterAnalyzer.ExtractForNavigation(group, "Orders");

        result.Should().NotBeNull();
        result!.Children.Should().ContainSingle()
            .Which.Should().BeOfType<FilterConditionNode>()
            .Which.Field.Should().Be("Total");
    }
}
