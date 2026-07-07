using FlexQuery.NET.Models;

namespace FlexQuery.NET.Tests.Tests;

public class FlexQueryBaseTests
{
    [Fact]
    public void FlexQueryRequest_IsFlexQueryBase()
    {
        var request = new FlexQueryRequest
        {
            Filter = "Age gt 18",
            Sort = "Name:asc",
            Select = "Id,Name",
            Include = "Orders",
            GroupBy = "Category",
            Having = "count(Id) gt 5",
            Page = 2,
            PageSize = 25,
            IncludeCount = false,
            Distinct = true,
            Mode = "Flat"
        };

        request.Filter.Should().Be("Age gt 18");
        request.Sort.Should().Be("Name:asc");
        request.Select.Should().Be("Id,Name");
        request.Include.Should().Be("Orders");
        request.GroupBy.Should().Be("Category");
        request.Having.Should().Be("count(Id) gt 5");
        request.Page.Should().Be(2);
        request.PageSize.Should().Be(25);
        request.IncludeCount.Should().BeFalse();
        request.Distinct.Should().BeTrue();
        request.Mode.Should().Be("Flat");
    }

    [Fact]
    public void FlexQueryRequest_IncludesAlias_IsObsolete()
    {
        var request = new FlexQueryRequest();
        request.Includes = "Orders,Profile";
        request.Include.Should().Be("Orders,Profile");
    }

    [Fact]
    public void FlexQueryParameters_IsFlexQueryBase()
    {
        var parameters = new FlexQueryParameters
        {
            Filter = "Name eq John",
            Page = 1,
            PageSize = 10,
            RawParameters = new Dictionary<string, string> { { "$filter", "Name eq John" } }
        };

        parameters.Filter.Should().Be("Name eq John");
        parameters.Page.Should().Be(1);
        parameters.PageSize.Should().Be(10);
        parameters.RawParameters.Should().ContainKey("$filter");
    }

    [Fact]
    public void FlexQueryParameters_RawParameters_CanBeNull()
    {
        var parameters = new FlexQueryParameters();
        parameters.RawParameters.Should().BeNull();
    }
}