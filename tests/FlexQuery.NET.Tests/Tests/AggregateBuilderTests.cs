using FlexQuery.NET.Builders;
using FlexQuery.NET.Builders.Fluent;

namespace FlexQuery.NET.Tests.Tests;

public class AggregateBuilderTests
{
    [Fact]
    public void Build_WithNoAggregates_ReturnsEmpty()
    {
        var builder = new AggregateBuilder();
        var result = builder.Build();

        result.Should().BeEmpty();
    }

    [Fact]
    public void Count_WithoutField_SetsFieldNull()
    {
        var builder = new AggregateBuilder();
        builder.Count("Total");
        var result = builder.Build();

        result.Should().ContainSingle();
        result[0].Function.Should().Be("count");
        result[0].Field.Should().BeNull();
        result[0].Alias.Should().Be("Total");
    }
}