using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Parsers.Fql;
using Xunit;

namespace FlexQuery.NET.Tests.Parsers.Fql;

public class FqlSortParserTests
{
    private static List<SortNode> Parse(string? sortRaw) =>
        FqlSortParser.Parse(sortRaw);

    [Fact]
    public void Parse_NullOrEmpty_ReturnsEmptyList()
    {
        Parse(null).Should().BeEmpty();
        Parse("").Should().BeEmpty();
        Parse("   ").Should().BeEmpty();
    }

    [Fact]
    public void Parse_SingleFieldAsc_DefaultsToAscending()
    {
        var result = Parse("CustomerName");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("CustomerName");
        result[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_SingleFieldDesc_SetsDescending()
    {
        var result = Parse("CustomerName DESC");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("CustomerName");
        result[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void Parse_MultipleFields_ParsedCorrectly()
    {
        var result = Parse("CustomerName DESC, CreatedDate ASC");

        result.Should().HaveCount(2);
        result[0].Field.Should().Be("CustomerName");
        result[0].Descending.Should().BeTrue();
        result[1].Field.Should().Be("CreatedDate");
        result[1].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_Direction_CaseInsensitive()
    {
        var result = Parse("Name desc, Age ASC");

        result[0].Descending.Should().BeTrue();
        result[1].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_NestedPropertyPath_ParsedCorrectly()
    {
        var result = Parse("Orders.Customer.Name ASC");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Orders.Customer.Name");
        result[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_InvalidDirection_Throws()
    {
        var act = () => Parse("Name SIDEWAYS");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_InvalidFieldPath_Throws()
    {
        var act = () => Parse("Name. ASC");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_AggregateSum_Desc_ParsedCorrectly()
    {
        var result = Parse("SUM(Orders.Total) DESC");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Orders");
        result[0].Aggregate.Should().Be(AggregateFunction.Sum);
        result[0].AggregateField.Should().Be("Total");
        result[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void Parse_AggregateSum_DefaultAsc_ParsedCorrectly()
    {
        var result = Parse("SUM(Orders.Total)");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Orders");
        result[0].Aggregate.Should().Be(AggregateFunction.Sum);
        result[0].AggregateField.Should().Be("Total");
        result[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_AggregateCount_NoField_ParsedCorrectly()
    {
        var result = Parse("COUNT(Orders) ASC");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Orders");
        result[0].Aggregate.Should().Be(AggregateFunction.Count);
        result[0].AggregateField.Should().BeNull();
        result[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_Aggregate_CaseInsensitive_ParsedCorrectly()
    {
        var result = Parse("sum(Orders.Total) desc, AVG(Orders.Price) ASC");

        result.Should().HaveCount(2);
        result[0].Field.Should().Be("Orders");
        result[0].Aggregate.Should().Be(AggregateFunction.Sum);
        result[0].AggregateField.Should().Be("Total");
        result[0].Descending.Should().BeTrue();
        result[1].Field.Should().Be("Orders");
        result[1].Aggregate.Should().Be(AggregateFunction.Avg);
        result[1].AggregateField.Should().Be("Price");
        result[1].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_Aggregate_InvalidDirection_Throws()
    {
        var act = () => Parse("SUM(Orders.Total) SIDEWAYS");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_Aggregate_UnknownFunction_Throws()
    {
        var act = () => Parse("UNKNOWN(Orders.Total) DESC");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_Aggregate_MissingField_Throws()
    {
        var act = () => Parse("SUM() DESC");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_Aggregate_MissingFunction_Throws()
    {
        var act = () => Parse("(Orders.Total) DESC");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_Aggregate_MissingClosingParen_Throws()
    {
        var act = () => Parse("SUM(Orders.Total DESC");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_Aggregate_ExtraContent_Throws()
    {
        var act = () => Parse("SUM(Orders.Total) DESC EXTRA");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_EmptyItemInCsv_Ignored()
    {
        var result = Parse("Name ASC,,Age DESC");

        result.Should().HaveCount(2);
        result[0].Field.Should().Be("Name");
        result[1].Field.Should().Be("Age");
    }

    [Fact]
    public void Parse_LeadingComma_Ignored()
    {
        var result = Parse(",Name ASC");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Name");
    }

    [Fact]
    public void Parse_TrailingComma_Ignored()
    {
        var result = Parse("Name ASC,");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Name");
    }

    [Fact]
    public void Parse_DuplicateCommas_Ignored()
    {
        var result = Parse("Name ASC,,Age DESC,,CreatedDate ASC");

        result.Should().HaveCount(3);
        result[0].Field.Should().Be("Name");
        result[1].Field.Should().Be("Age");
        result[2].Field.Should().Be("CreatedDate");
    }

    [Fact]
    public void Parse_MixedFieldAndAggregateSorts_ParsedCorrectly()
    {
        var result = Parse("CustomerName ASC, SUM(Orders.Total) DESC, CreatedDate ASC");

        result.Should().HaveCount(3);
        result[0].Field.Should().Be("CustomerName");
        result[0].Aggregate.Should().BeNull();
        result[1].Field.Should().Be("Orders");
        result[1].Aggregate.Should().Be(AggregateFunction.Sum);
        result[1].AggregateField.Should().Be("Total");
        result[1].Descending.Should().BeTrue();
        result[2].Field.Should().Be("CreatedDate");
        result[2].Aggregate.Should().BeNull();
    }

    [Fact]
    public void Parse_AggregateCount_Star_NotSupported_Throws()
    {
        var act = () => Parse("COUNT(*) DESC");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*COUNT(*) is not supported in sort*");
    }

    [Fact]
    public void Parse_AggregateSum_RootProperty_ParsedCorrectly()
    {
        var result = Parse("SUM(Price) DESC");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Price");
        result[0].Aggregate.Should().Be(AggregateFunction.Sum);
        result[0].AggregateField.Should().BeNull();
        result[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void Parse_AggregateAvg_RootProperty_ParsedCorrectly()
    {
        var result = Parse("AVG(Price) ASC");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Price");
        result[0].Aggregate.Should().Be(AggregateFunction.Avg);
        result[0].AggregateField.Should().BeNull();
        result[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_AggregateMin_RootProperty_ParsedCorrectly()
    {
        var result = Parse("MIN(Price) DESC");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Price");
        result[0].Aggregate.Should().Be(AggregateFunction.Min);
        result[0].AggregateField.Should().BeNull();
        result[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void Parse_AggregateMax_RootProperty_ParsedCorrectly()
    {
        var result = Parse("MAX(Price) ASC");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Price");
        result[0].Aggregate.Should().Be(AggregateFunction.Max);
        result[0].AggregateField.Should().BeNull();
        result[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_Aggregate_DeepNavigation_ParsedCorrectly()
    {
        var result = Parse("SUM(Customer.Region.Sales.Total) DESC");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Customer.Region.Sales");
        result[0].Aggregate.Should().Be(AggregateFunction.Sum);
        result[0].AggregateField.Should().Be("Total");
        result[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void Parse_AggregateCount_CollectionNavigation_ParsedCorrectly()
    {
        var result = Parse("COUNT(Customer.Orders) ASC");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Customer.Orders");
        result[0].Aggregate.Should().Be(AggregateFunction.Count);
        result[0].AggregateField.Should().BeNull();
        result[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_AggregateMin_ParsedCorrectly()
    {
        var result = Parse("MIN(Orders.Price) ASC");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Orders");
        result[0].Aggregate.Should().Be(AggregateFunction.Min);
        result[0].AggregateField.Should().Be("Price");
        result[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_AggregateMax_ParsedCorrectly()
    {
        var result = Parse("MAX(Orders.Price) DESC");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Orders");
        result[0].Aggregate.Should().Be(AggregateFunction.Max);
        result[0].AggregateField.Should().Be("Price");
        result[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void Parse_AggregateAvg_ParsedCorrectly()
    {
        var result = Parse("AVG(Orders.Rating) ASC");

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Orders");
        result[0].Aggregate.Should().Be(AggregateFunction.Avg);
        result[0].AggregateField.Should().Be("Rating");
        result[0].Descending.Should().BeFalse();
    }
}