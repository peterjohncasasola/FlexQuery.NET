using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Dsl;

namespace FlexQuery.NET.Tests.Parsers.Dsl;

public class DslAggregateParserTests
{
    private static List<Aggregate> Parse(string? raw) =>
        DslAggregateParser.Parse(raw);

    [Fact]
    public void Parse_Null_ReturnsEmptyList()
    {
        Parse(null).Should().BeEmpty();
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyList()
    {
        Parse("").Should().BeEmpty();
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsEmptyList()
    {
        Parse("   ").Should().BeEmpty();
    }

    [Fact]
    public void Parse_SingleAggregate_NoAlias_GeneratesDefaultAlias()
    {
        var result = Parse("sum:Amount");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[0].Field.Should().Be("Amount");
        result[0].Alias.Should().Be("AmountSum");
    }

    [Fact]
    public void Parse_SingleAggregate_WithAlias_UsesExplicitAlias()
    {
        var result = Parse("sum:Amount:TotalSales");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[0].Field.Should().Be("Amount");
        result[0].Alias.Should().Be("TotalSales");
    }

    [Fact]
    public void Parse_SingleAggregate_Count_NoAlias_GeneratesDefaultAlias()
    {
        var result = Parse("count:Id");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Count);
        result[0].Field.Should().Be("Id");
        result[0].Alias.Should().Be("IdCount");
    }

    [Fact]
    public void Parse_SingleAggregate_Count_WithAlias()
    {
        var result = Parse("count:Id:OrderCount");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Count);
        result[0].Field.Should().Be("Id");
        result[0].Alias.Should().Be("OrderCount");
    }

    [Fact]
    public void Parse_SingleAggregate_AvgFunction_NoAlias()
    {
        var result = Parse("avg:Price");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Avg);
        result[0].Field.Should().Be("Price");
        result[0].Alias.Should().Be("PriceAvg");
    }

    [Fact]
    public void Parse_SingleAggregate_MinFunction_NoAlias()
    {
        var result = Parse("min:Date");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Min);
        result[0].Field.Should().Be("Date");
        result[0].Alias.Should().Be("DateMin");
    }

    [Fact]
    public void Parse_SingleAggregate_MaxFunction_NoAlias()
    {
        var result = Parse("max:Date");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Max);
        result[0].Field.Should().Be("Date");
        result[0].Alias.Should().Be("DateMax");
    }

    [Fact]
    public void Parse_MultipleAggregates_ParsedCorrectly()
    {
        var result = Parse("sum:Amount,avg:Price,count:Id");

        result.Should().HaveCount(3);
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[0].Field.Should().Be("Amount");
        result[0].Alias.Should().Be("AmountSum");
        result[1].Function.Should().Be(AggregateFunction.Avg);
        result[1].Field.Should().Be("Price");
        result[1].Alias.Should().Be("PriceAvg");
        result[2].Function.Should().Be(AggregateFunction.Count);
        result[2].Field.Should().Be("Id");
        result[2].Alias.Should().Be("IdCount");
    }

    [Fact]
    public void Parse_MultipleAggregates_WithMixedAliases()
    {
        var result = Parse("sum:Amount:Total,avg:Price,count:Id:Orders");

        result.Should().HaveCount(3);
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[0].Field.Should().Be("Amount");
        result[0].Alias.Should().Be("Total");
        result[1].Function.Should().Be(AggregateFunction.Avg);
        result[1].Field.Should().Be("Price");
        result[1].Alias.Should().Be("PriceAvg");
        result[2].Function.Should().Be(AggregateFunction.Count);
        result[2].Field.Should().Be("Id");
        result[2].Alias.Should().Be("Orders");
    }

    [Fact]
    public void Parse_NestedField_AliasGeneratedCorrectly()
    {
        var result = Parse("sum:Orders.Total");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[0].Field.Should().Be("Orders.Total");
        result[0].Alias.Should().Be("OrdersTotalSum");
    }

    [Fact]
    public void Parse_DeepNestedField_AliasGeneratedCorrectly()
    {
        var result = Parse("avg:Customer.Region.Sales.Price");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Avg);
        result[0].Field.Should().Be("Customer.Region.Sales.Price");
        result[0].Alias.Should().Be("CustomerRegionSalesPriceAvg");
    }

    [Fact]
    public void Parse_FunctionNames_CaseInsensitive()
    {
        var result = Parse("SUM:Amount,Avg:Price,COUNT:Id");

        result.Should().HaveCount(3);
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[1].Function.Should().Be(AggregateFunction.Avg);
        result[2].Function.Should().Be(AggregateFunction.Count);
    }

    [Fact]
    public void Parse_Alias_ValidIdentifier_Accepted()
    {
        var result = Parse("sum:Amount:TotalSales");

        result.Should().ContainSingle();
        result[0].Alias.Should().Be("TotalSales");
    }

    [Fact]
    public void Parse_Alias_SameAsField_Accepted()
    {
        var result = Parse("sum:Amount:Amount");

        result.Should().ContainSingle();
        result[0].Alias.Should().Be("Amount");
    }

    [Fact]
    public void Parse_Alias_StartsWithUnderscore_ThrowsDslParseException()
    {
        var ex = Record.Exception(() => Parse("sum:Amount:_Total"));

        ex.Should().BeOfType<DslParseException>();
        ex.Message.Should().Contain("Invalid alias");
    }

    [Fact]
    public void Parse_Alias_StartsWithDigit_ThrowsDslParseException()
    {
        var ex = Record.Exception(() => Parse("sum:Amount:1Total"));

        ex.Should().BeOfType<DslParseException>();
        ex.Message.Should().Contain("Invalid alias");
    }

    [Fact]
    public void Parse_Alias_ContainsSpace_ThrowsDslParseException()
    {
        var ex = Record.Exception(() => Parse("sum:Amount:Total Sales"));

        ex.Should().BeOfType<DslParseException>();
        ex.Message.Should().Contain("Invalid alias");
    }

    [Fact]
    public void Parse_Alias_ContainsHyphen_ThrowsDslParseException()
    {
        var ex = Record.Exception(() => Parse("sum:Amount:Total-Sales"));

        ex.Should().BeOfType<DslParseException>();
        ex.Message.Should().Contain("Invalid alias");
    }

    [Fact]
    public void Parse_Alias_ReservedKeyword_AcceptedAtParserLevel()
    {
        var result = Parse("sum:Amount:select");

        result.Should().ContainSingle();
        result[0].Alias.Should().Be("select");
    }

    [Fact]
    public void Parse_MissingFunction_ThrowsDslParseException()
    {
        var ex = Record.Exception(() => Parse(":Amount"));

        ex.Should().BeOfType<DslParseException>();
        ex.Message.Should().Contain("Missing function");
    }

    [Fact]
    public void Parse_MissingField_ThrowsDslParseException()
    {
        var ex = Record.Exception(() => Parse("sum:"));

        ex.Should().BeOfType<DslParseException>();
        ex.Message.Should().Contain("Missing field");
    }

    [Fact]
    public void Parse_EmptyAlias_ThrowsDslParseException()
    {
        var ex = Record.Exception(() => Parse("sum:Amount:"));

        ex.Should().BeOfType<DslParseException>();
        ex.Message.Should().Contain("Empty alias");
    }

    [Fact]
    public void Parse_TooManyParts_ThrowsDslParseException()
    {
        var ex = Record.Exception(() => Parse("sum:Amount:Alias:Extra"));

        ex.Should().BeOfType<DslParseException>();
        ex.Message.Should().Contain("Too many parts");
    }

    [Fact]
    public void Parse_InvalidFunction_ThrowsDslParseException()
    {
        var ex = Record.Exception(() => Parse("invalid:Amount"));

        ex.Should().BeOfType<DslParseException>();
        ex.Message.Should().Contain("Unrecognized aggregate function");
    }

    [Fact]
    public void Parse_CountStar_ThrowsDslParseException()
    {
        var ex = Record.Exception(() => Parse("count:*"));

        ex.Should().BeOfType<DslParseException>();
        ex.Message.Should().Contain("count:* is not supported");
    }

    [Fact]
    public void Parse_LeadingComma_ThrowsDslParseException()
    {
        var ex = Record.Exception(() => Parse(",sum:Amount"));

        ex.Should().BeOfType<DslParseException>();
        ex.Message.Should().Contain("Empty aggregate item found");
    }

    [Fact]
    public void Parse_TrailingComma_ThrowsDslParseException()
    {
        var ex = Record.Exception(() => Parse("sum:Amount,"));

        ex.Should().BeOfType<DslParseException>();
        ex.Message.Should().Contain("Empty aggregate item found");
    }

    [Fact]
    public void Parse_EmptyItemBetweenCommas_ThrowsDslParseException()
    {
        var ex = Record.Exception(() => Parse("sum:Amount,,avg:Price"));

        ex.Should().BeOfType<DslParseException>();
        ex.Message.Should().Contain("Empty aggregate item found");
    }

    [Fact]
    public void Parse_MissingColon_ThrowsDslParseException()
    {
        var ex = Record.Exception(() => Parse("sum"));

        ex.Should().BeOfType<DslParseException>();
        ex.Message.Should().Contain("Expected format: Function:Field[:Alias]");
    }

    [Fact]
    public void Parse_ExtraWhitespaceAroundParts_Handled()
    {
        var result = Parse("  sum : Amount : TotalSales  ");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[0].Field.Should().Be("Amount");
        result[0].Alias.Should().Be("TotalSales");
    }

    [Fact]
    public void Parse_AverageAlias_GeneratesPriceAvg()
    {
        var result = Parse("average:Price");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Avg);
        result[0].Field.Should().Be("Price");
        result[0].Alias.Should().Be("PriceAvg");
    }

    [Fact]
    public void Parse_Alias_WhitespaceOnlyAlias_ThrowsDslParseException()
    {
        var ex = Record.Exception(() => Parse("sum:Amount:  "));

        ex.Should().BeOfType<DslParseException>();
        ex.Message.Should().Contain("Empty alias");
    }

    [Fact]
    public void Parse_Field_StartingWithDot_ThrowsDslParseException()
    {
        var ex = Record.Exception(() => Parse("sum:.Orders.Total"));

        ex.Should().BeOfType<DslParseException>();
        ex.Message.Should().Contain("Invalid field");
    }

    [Fact]
    public void Parse_Field_EndingWithDot_ThrowsDslParseException()
    {
        var ex = Record.Exception(() => Parse("sum:Orders.Total."));

        ex.Should().BeOfType<DslParseException>();
        ex.Message.Should().Contain("Invalid field");
    }

    [Fact]
    public void Parse_Field_DoubleDot_ThrowsDslParseException()
    {
        var ex = Record.Exception(() => Parse("sum:Orders..Total"));

        ex.Should().BeOfType<DslParseException>();
        ex.Message.Should().Contain("Invalid field");
    }

    [Fact]
    public void Parse_Count_WithCollectionPath_Accepted()
    {
        var result = Parse("count:Orders.Total");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Count);
        result[0].Field.Should().Be("Orders.Total");
        result[0].Alias.Should().Be("OrdersTotalCount");
    }

    [Fact]
    public void Parse_FunctionOrder_Preserved()
    {
        var result = Parse("sum:Amount,avg:Price");

        result.Should().HaveCount(2);
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[1].Function.Should().Be(AggregateFunction.Avg);
    }

    [Fact]
    public void Parse_AutoAlias_CasingPreserved()
    {
        var result = Parse("SUM:Amount");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[0].Field.Should().Be("Amount");
        result[0].Alias.Should().Be("AmountSum");
    }
    
    [Fact]
    public void Parse_Field_CountStar_ThrowsFqlParseException()
    {
        var ex = Record.Exception(() => Parse("count:*"));

        ex.Should().BeOfType<DslParseException>();
    }
}
