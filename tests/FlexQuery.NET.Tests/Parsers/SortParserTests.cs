using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Dsl;
using Xunit;

namespace FlexQuery.NET.Tests.Parsers;

public class SortParserTests
{
    [Fact]
    public void Parse_SingleFieldAsc_DefaultsToAscending()
    {
        var result = SortParser.Parse("LastName");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("LastName");
        result[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_FieldWithDirection()
    {
        var asc = SortParser.Parse("LastName:asc");
        var desc = SortParser.Parse("LastName:desc");

        asc[0].Descending.Should().BeFalse();
        desc[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void Parse_MultipleFields()
    {
        var result = SortParser.Parse("LastName:asc,FirstName:desc");

        result.Should().HaveCount(2);
        result[0].Field.Should().Be("LastName");
        result[0].Descending.Should().BeFalse();
        result[1].Field.Should().Be("FirstName");
        result[1].Descending.Should().BeTrue();
    }

    [Fact]
    public void Parse_InvalidDirection_Throws()
    {
        var act = () => SortParser.Parse("LastName:sideways");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_EmptyItem_Throws()
    {
        var act = () => SortParser.Parse("LastName:asc,,");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_InvalidFieldPath_Throws()
    {
        var act = () => SortParser.Parse("Name.:asc");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_AggregateSort_SetsAggregateFields()
    {
        var result = SortParser.Parse("Orders.sum(total):desc");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Orders");
        result[0].Aggregate.Should().Be("sum");
        result[0].AggregateField.Should().Be("total");
        result[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void Parse_AggregateSort_WithoutField()
    {
        var result = SortParser.Parse("Orders.count():asc");

        result[0].Aggregate.Should().Be("count");
        result[0].AggregateField.Should().BeNull();
        result[0].Descending.Should().BeFalse();
    }
}
