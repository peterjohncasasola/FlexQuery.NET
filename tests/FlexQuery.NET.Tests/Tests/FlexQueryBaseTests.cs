using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Tests.Tests;

public class FlexQueryBaseTests
{
    [Fact]
    public void FlexQueryRequest_PopulatesAllProperties()
    {
        var request = new FlexQueryRequest
        {
            Filter = new FilterGroup
            {
                Logic = LogicOperator.And,
                Filters = [new FilterCondition { Field = "Age", Operator = "gt", Value = "18" }]
            },
            Sort = [new SortNode { Field = "Name", Descending = false }],
            Select = ["Id", "Name"],
            Includes = ["Orders"],
            GroupBy = ["Category"],
            Having = new HavingCondition { Function = AggregateFunction.Count, Field = "Id", Operator = "gt", Value = "5" },
            Paging = new PagingOptions { Page = 2, PageSize = 25 },
            IncludeCount = false,
            Distinct = true,
            ProjectionMode = ProjectionMode.Flat
        };

        request.Filter.Should().NotBeNull();
        request.Filter!.Filters.Should().Contain(f => f.Field == "Age" && f.Operator == "gt" && f.Value == "18");
        request.Sort.Should().Contain(s => s.Field == "Name" && !s.Descending);
        request.Select.Should().BeEquivalentTo("Id", "Name");
        request.Includes.Should().BeEquivalentTo("Orders");
        request.GroupBy.Should().BeEquivalentTo("Category");
        request.Having.Should().NotBeNull();
        request.Having!.Function.Should().Be(AggregateFunction.Count);
        request.Having.Field.Should().Be("Id");
        request.Paging.Page.Should().Be(2);
        request.Paging.PageSize.Should().Be(25);
        request.IncludeCount.Should().BeFalse();
        request.Distinct.Should().BeTrue();
        request.ProjectionMode.Should().Be(ProjectionMode.Flat);
    }

    [Fact]
    public void FlexQueryParameters_IsFlexQueryBase()
    {
        var parameters = new FlexQueryParameters
        {
            Filter = "Name eq John",
            Page = 1,
            PageSize = 10
        };

        parameters.Filter.Should().Be("Name eq John");
        parameters.Page.Should().Be(1);
        parameters.PageSize.Should().Be(10);
    }
}
