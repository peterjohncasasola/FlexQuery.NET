using FlexQuery.NET.Builders.Fluent;
using FlexQuery.NET.Models.Aggregates;

namespace FlexQuery.NET.Tests.Builders;

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
    public void Count_WithField_SetsFieldCorrectly()
    {
        var builder = new AggregateBuilder();
        builder.Count("Id", "Total");
        var result = builder.Build();

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Count);
        result[0].Field.Should().Be("Id");
        result[0].Alias.Should().Be("Total");
    }

    [Fact]
    public void Sum_WithExplicitAlias_SetsAlias()
    {
        var builder = new AggregateBuilder();
        builder.Sum("Amount", "TotalSales");
        var result = builder.Build();

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[0].Field.Should().Be("Amount");
        result[0].Alias.Should().Be("TotalSales");
    }

    [Fact]
    public void Sum_WithNullAlias_AutoGeneratesAlias()
    {
        var builder = new AggregateBuilder();
        builder.Sum("Amount", null);
        var result = builder.Build();

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[0].Field.Should().Be("Amount");
        result[0].Alias.Should().Be("AmountSum");
    }

    [Fact]
    public void Sum_WithEmptyAlias_AutoGeneratesAlias()
    {
        var builder = new AggregateBuilder();
        builder.Sum("Amount", "");
        var result = builder.Build();

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[0].Field.Should().Be("Amount");
        result[0].Alias.Should().Be("AmountSum");
    }
}
