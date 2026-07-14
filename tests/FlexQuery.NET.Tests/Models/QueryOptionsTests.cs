using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Tests.Models;

public class QueryOptionsTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultValues()
    {
        var options = new QueryOptions();

        options.Sort.Should().BeEmpty();
        options.Aggregates.Should().BeEmpty();
        options.Paging.Should().NotBeNull();
        options.IncludeCount.Should().BeTrue();
        options.ProjectionMode.Should().Be(ProjectionMode.Nested);
    }

    [Fact]
    public void Filter_NullByDefault()
    {
        var options = new QueryOptions();
        options.Filter.Should().BeNull();
    }

    [Fact]
    public void Select_NullByDefault()
    {
        var options = new QueryOptions();
        options.Select.Should().BeNull();
    }

    [Fact]
    public void Includes_NullByDefault()
    {
        var options = new QueryOptions();
        options.Includes.Should().BeNull();
    }

    [Fact]
    public void Expand_NullByDefault()
    {
        var options = new QueryOptions();
        options.Expand.Should().BeNull();
    }

    [Fact]
    public void GroupBy_NullByDefault()
    {
        var options = new QueryOptions();
        options.GroupBy.Should().BeNull();
    }

    [Fact]
    public void Having_NullByDefault()
    {
        var options = new QueryOptions();
        options.Having.Should().BeNull();
    }

    [Fact]
    public void Distinct_NullByDefault()
    {
        var options = new QueryOptions();
        options.Distinct.Should().BeNull();
    }
}
