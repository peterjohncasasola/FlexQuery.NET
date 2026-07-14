using FlexQuery.NET.Models;
using FlexQuery.NET.OpenApi.Documentation;
using Xunit;

namespace FlexQuery.NET.OpenApi.Tests.Documentation;

public class ExampleProviderTests
{
    [Fact]
    public void CreateRequestExample_IsConsistent()
    {
        var example = ExampleProvider.CreateRequestExample();

        example.Should().NotBeNull();
        example.Filter.Should().NotBeNull();
        example.Sort.Should().NotBeEmpty();
        example.Aggregate.Should().NotBeEmpty();
        example.Paging.Should().NotBeNull();
    }

    [Fact]
    public void CreateParametersExample_AliasesMatchAggregates()
    {
        var example = ExampleProvider.CreateParametersExample();

        example.Should().NotBeNull();
        example.Aggregate.Should().Contain("TotalRevenue");
        example.Aggregate.Should().Contain("OrderCount");
        example.Aggregate.Should().Contain("AvgRating");
        example.Having.Should().Contain("TotalAmount");
    }

    [Fact]
    public void CreateQueryResultExample_HasDataAndAggregates()
    {
        var example = ExampleProvider.CreateQueryResultExample();

        example.Should().NotBeNull();
        example.Data.Should().NotBeEmpty();
        example.Aggregates.Should().NotBeEmpty();
        example.TotalCount.Should().Be(125);
    }
}
