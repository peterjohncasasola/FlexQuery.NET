using FlexQuery.NET.Builders;
using FlexQuery.NET.Models.Aggregates;

namespace FlexQuery.NET.Tests.Builders;

public class AggregateResultBuilderTests
{
    private sealed class AggregateRow
    {
        public double TotalSum { get; set; }
        public int IdCount { get; set; }
    }

    [Fact]
    public void Build_NullRow_ReturnsNull()
    {
        var result = AggregateResultBuilder.Build(null, []);
        result.Should().BeNull();
    }

    [Fact]
    public void Build_EmptyAggregates_ReturnsNull()
    {
        var row = new AggregateRow { TotalSum = 100, IdCount = 5 };

        var result = AggregateResultBuilder.Build(row, []);

        result.Should().BeNull();
    }

    [Fact]
    public void Build_MatchingAggregates_ReturnsGrandTotals()
    {
        var row = new AggregateRow { TotalSum = 250.0, IdCount = 10 };
        var aggregates = new List<AggregateModel>
        {
            new() { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" },
            new() { Function = AggregateFunction.Count, Field = "Id", Alias = "IdCount" }
        };

        var result = AggregateResultBuilder.Build(row, aggregates);

        result.Should().NotBeNull();
        result!.Should().ContainKey("Total");
        result["Total"].Should().ContainKey("sum");
        result["Total"]["sum"].Should().Be(250.0);
        result.Should().ContainKey("Id");
        result["Id"].Should().ContainKey("count");
        result["Id"]["count"].Should().Be(10);
    }

    [Fact]
    public void Build_PartialMatch_OnlyReturnsMatching()
    {
        var row = new AggregateRow { TotalSum = 100, IdCount = 5 };
        var aggregates = new List<AggregateModel>
        {
            new() { Function = AggregateFunction.Sum, Field = "Total", Alias = "TotalSum" }
        };

        var result = AggregateResultBuilder.Build(row, aggregates);

        result.Should().NotBeNull();
        result!.Should().ContainKey("Total");
        result.Should().NotContainKey("Id");
    }

    [Fact]
    public void Build_MissingAlias_ReturnsNullForUnmatchedProperty()
    {
        var row = new { OtherField = 42 };
        var aggregates = new List<AggregateModel>
        {
            new() { Function = AggregateFunction.Count, Field = "Id", Alias = "NonExistentProperty" }
        };

        var result = AggregateResultBuilder.Build(row, aggregates);

        result.Should().BeNull();
    }
}
