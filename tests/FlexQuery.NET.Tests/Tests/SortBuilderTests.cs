using FlexQuery.NET.Builders.Fluent;
using FlexQuery.NET.Models.Paging;

namespace FlexQuery.NET.Tests.Tests;

public class SortBuilderTests
{
    [Fact]
    public void Build_WithNoSorts_ReturnsEmpty()
    {
        var builder = new SortBuilder();
        var result = builder.Build();

        result.Should().BeEmpty();
    }

    [Fact]
    public void Ascending_AndDescending_AddsSortNodes()
    {
        var builder = new SortBuilder();
        builder.Ascending("Name").Descending("Age");
        var result = builder.Build();

        result.Should().HaveCount(2);
        result[0].Should().Match<SortNode>(s => s.Field == "Name" && !s.Descending);
        result[1].Should().Match<SortNode>(s => s.Field == "Age" && s.Descending);
    }
}