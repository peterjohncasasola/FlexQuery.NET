using FlexQuery.NET.Builders;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;

namespace FlexQuery.NET.Tests.Builders;

public class GroupByBuilderTests
{
    private sealed class TestItem
    {
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    [Fact]
    public void GetProjectionName_SimpleField_ReturnsField()
    {
        GroupByBuilder.GetProjectionName("Name").Should().Be("Name");
    }

    [Fact]
    public void GetProjectionName_NestedField_ReturnsLastSegment()
    {
        GroupByBuilder.GetProjectionName("Profile.Name").Should().Be("Name");
    }

    [Fact]
    public void GetProjectionName_RemovesUnderscores()
    {
        var result = GroupByBuilder.GetProjectionName("my_field");
        result.Should().NotContain("_");
    }
}
