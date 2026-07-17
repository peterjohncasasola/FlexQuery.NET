using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Dsl;
using Xunit;

namespace FlexQuery.NET.Tests.Parsers;

public class DslSortParserTests
{
    [Fact]
    public void Parse_SingleFieldAsc_DefaultsToAscending()
    {
        var result = DslSortParser.Parse("LastName");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("LastName");
        result[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_FieldWithDirection()
    {
        var asc = DslSortParser.Parse("LastName:asc");
        var desc = DslSortParser.Parse("LastName:desc");

        asc[0].Descending.Should().BeFalse();
        desc[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void Parse_MultipleFields()
    {
        var result = DslSortParser.Parse("LastName:asc,FirstName:desc");

        result.Should().HaveCount(2);
        result[0].Field.Should().Be("LastName");
        result[0].Descending.Should().BeFalse();
        result[1].Field.Should().Be("FirstName");
        result[1].Descending.Should().BeTrue();
    }

    [Fact]
    public void Parse_InvalidDirection_Throws()
    {
        var act = () => DslSortParser.Parse("LastName:sideways");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_EmptyItem_Throws()
    {
        var act = () => DslSortParser.Parse("LastName:asc,,");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_InvalidFieldPath_Throws()
    {
        var act = () => DslSortParser.Parse("Name.:asc");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_AggregateSort_SetsAggregateFields()
    {
        var result = DslSortParser.Parse("sum:Orders.Total:desc");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Orders");
        result[0].Aggregate.Should().Be(AggregateFunction.Sum);
        result[0].AggregateField.Should().Be("Total");
        result[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void Parse_AggregateSort_Count_WithoutField()
    {
        var result = DslSortParser.Parse("count:Orders:asc");

        result[0].Aggregate.Should().Be(AggregateFunction.Count);
        result[0].AggregateField.Should().BeNull();
        result[0].Field.Should().Be("Orders");
        result[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_AggregateSort_DefaultsToAscending()
    {
        var result = DslSortParser.Parse("count:Orders");

        result.Should().ContainSingle();
        result[0].Aggregate.Should().Be(AggregateFunction.Count);
        result[0].Field.Should().Be("Orders");
        result[0].AggregateField.Should().BeNull();
        result[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_AggregateSort_WithAscendingDirection()
    {
        var result = DslSortParser.Parse("count:Orders:asc");

        result[0].Aggregate.Should().Be(AggregateFunction.Count);
        result[0].Field.Should().Be("Orders");
        result[0].AggregateField.Should().BeNull();
        result[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_AggregateSort_WithDescendingDirection()
    {
        var result = DslSortParser.Parse("count:Orders:desc");

        result[0].Aggregate.Should().Be(AggregateFunction.Count);
        result[0].Field.Should().Be("Orders");
        result[0].AggregateField.Should().BeNull();
        result[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void Parse_AggregateSort_RootMax_Desc()
    {
        var result = DslSortParser.Parse("max:Price:desc");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Price");
        result[0].Aggregate.Should().Be(AggregateFunction.Max);
        result[0].AggregateField.Should().BeNull();
        result[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void Parse_AggregateSort_RootMin_DefaultsToAscending()
    {
        var result = DslSortParser.Parse("min:Price");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Price");
        result[0].Aggregate.Should().Be(AggregateFunction.Min);
        result[0].AggregateField.Should().BeNull();
        result[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_AggregateSort_RootSum_DefaultsToAscending()
    {
        var result = DslSortParser.Parse("sum:Price");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Price");
        result[0].Aggregate.Should().Be(AggregateFunction.Sum);
        result[0].AggregateField.Should().BeNull();
        result[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_AggregateSort_RootAvg_DefaultsToAscending()
    {
        var result = DslSortParser.Parse("avg:Price");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Price");
        result[0].Aggregate.Should().Be(AggregateFunction.Avg);
        result[0].AggregateField.Should().BeNull();
        result[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_AggregateSort_AvgWithNestedTarget()
    {
        var result = DslSortParser.Parse("avg:Orders.Rating");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Orders");
        result[0].Aggregate.Should().Be(AggregateFunction.Avg);
        result[0].AggregateField.Should().Be("Rating");
        result[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_AggregateSort_LegacySyntax_Throws()
    {
        var act = () => DslSortParser.Parse("Orders.sum(total):desc");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_AggregateSort_MissingTarget_Throws()
    {
        var act = () => DslSortParser.Parse("sum:");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_AggregateSort_InvalidTarget_Throws()
    {
        var act = () => DslSortParser.Parse("sum:Orders.:desc");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_AggregateSort_UnknownFunction_Throws()
    {
        var act = () => DslSortParser.Parse("unknown:Orders.Total:desc");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_AggregateSort_CountWithDottedTarget_IsAcceptedByParser()
    {
        var result = DslSortParser.Parse("count:Orders.Total");

        result.Should().ContainSingle();
        result[0].Aggregate.Should().Be(AggregateFunction.Count);
        result[0].Field.Should().Be("Orders.Total");
        result[0].AggregateField.Should().BeNull();
    }

    [Fact]
    public void Parse_AggregateSort_CountWithNestedDottedTarget_IsAcceptedByParser()
    {
        var result = DslSortParser.Parse("count:Customer.Orders.Total");

        result.Should().ContainSingle();
        result[0].Aggregate.Should().Be(AggregateFunction.Count);
        result[0].Field.Should().Be("Customer.Orders.Total");
        result[0].AggregateField.Should().BeNull();
    }
}
