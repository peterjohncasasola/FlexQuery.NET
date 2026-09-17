using FlexQuery.NET.Builders.Fluent;

namespace FlexQuery.NET.Tests.Builders;

public class IncludeBuilderTests
{
    [Fact]
    public void Build_WithNoPaths_ReturnsEmpty()
    {
        var builder = new IncludeBuilder();
        var result = builder.Build();

        result.Should().BeEmpty();
    }

    [Fact]
    public void Path_WithoutFilterOrChildren_AddsSimpleNode()
    {
        var builder = new IncludeBuilder();
        builder.Path("Orders");
        var result = builder.Build();

        result.Should().ContainSingle();
        result[0].Path.Should().Be("Orders");
        result[0].Filter.Should().BeNull();
        result[0].Children.Should().BeEmpty();
    }

    [Fact]
    public void Path_WithFilter_AddsFilteredNode()
    {
        var builder = new IncludeBuilder();
        builder.Path("Orders", f => f.Equal("Status", "Shipped"));
        var result = builder.Build();

        result.Should().ContainSingle();
        result[0].Filter.Should().NotBeNull();
        result[0].Filter!.Filters.Should().ContainSingle(c => c.Field == "Status" && c.Operator == "eq" && c.Value == "Shipped");
    }
}
