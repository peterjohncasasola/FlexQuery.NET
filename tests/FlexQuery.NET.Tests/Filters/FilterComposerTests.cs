using FlexQuery.NET.Filters;
using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Tests.Filters;

public class FilterComposerTests
{
    [Fact]
    public void MergeFilters_NullLeft_ReturnsRight()
    {
        var right = new FilterGroup { Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "test" }] };

        var result = FilterComposer.MergeFilters(null, right);

        result.Should().BeSameAs(right);
    }

    [Fact]
    public void MergeFilters_NullRight_ReturnsLeft()
    {
        var left = new FilterGroup { Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "test" }] };

        var result = FilterComposer.MergeFilters(left, null);

        result.Should().BeSameAs(left);
    }

    [Fact]
    public void MergeFilters_BothNull_ReturnsNull()
    {
        var result = FilterComposer.MergeFilters(null, null);

        result.Should().BeNull();
    }

    [Fact]
    public void MergeFilters_CombinesWithAndLogic()
    {
        var left = new FilterGroup { Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "Alice" }] };
        var right = new FilterGroup { Filters = [new FilterCondition { Field = "Age", Operator = "gt", Value = "25" }] };

        var result = FilterComposer.MergeFilters(left, right);

        result.Should().NotBeNull();
        result!.Logic.Should().Be(LogicOperator.And);
        result.Groups.Should().HaveCount(2);
        result.Groups[0].Should().BeSameAs(left);
        result.Groups[1].Should().BeSameAs(right);
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void MergeFilters_PreservesBothGroupsStructure()
    {
        var left = new FilterGroup
        {
            Logic = LogicOperator.Or,
            Filters =
            [
                new FilterCondition { Field = "Status", Operator = "eq", Value = "Active" },
                new FilterCondition { Field = "Status", Operator = "eq", Value = "Pending" }
            ]
        };
        var right = new FilterGroup
        {
            Filters = [new FilterCondition { Field = "Age", Operator = "gte", Value = "18" }]
        };

        var result = FilterComposer.MergeFilters(left, right);

        result!.Logic.Should().Be(LogicOperator.And);
        result.Groups.Should().HaveCount(2);
        result.Groups[0].Should().BeSameAs(left);
        result.Groups[1].Should().BeSameAs(right);
    }
}
