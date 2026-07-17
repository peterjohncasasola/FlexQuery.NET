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
        var result = DslSortParser.Parse("Orders.sum(total):desc");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Orders");
        result[0].Aggregate.Should().Be(AggregateFunction.Sum);
        result[0].AggregateField.Should().Be("total");
        result[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void Parse_AggregateSort_WithoutField()
    {
        var result = DslSortParser.Parse("Orders.count():asc");

        result[0].Aggregate.Should().Be(AggregateFunction.Count);
        result[0].AggregateField.Should().BeNull();
        result[0].Field.Should().Be("Orders");
        result[0].Descending.Should().BeFalse();
    }
    
    [Fact]
    public void Parse_AggregateSort_DefaultsToAscending()
    {
        var result = DslSortParser.Parse("Orders.count()");

        result.Should().ContainSingle();
        result[0].Aggregate.Should().Be(AggregateFunction.Count);
        result[0].Field.Should().Be("Orders");
        result[0].AggregateField.Should().BeNull();
        result[0].Descending.Should().BeFalse();
    }
    
    [Fact]
    public void Parse_AggregateSort_WithAscendingDirection()
    {
        var result = DslSortParser.Parse("Orders.count():asc");

        result[0].Aggregate.Should().Be(AggregateFunction.Count);
        result[0].Field.Should().Be("Orders");
        result[0].AggregateField.Should().BeNull();
        result[0].Descending.Should().BeFalse();
    }
    
    [Fact]
    public void Parse_AggregateSort_WithDescendingDirection()
    {
        var result = DslSortParser.Parse("Orders.count():desc");

        result[0].Aggregate.Should().Be(AggregateFunction.Count);
        result[0].Field.Should().Be("Orders");
        result[0].AggregateField.Should().BeNull();
        result[0].Descending.Should().BeTrue();
    }
}
