using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Parsers.Fql;
using Xunit;

namespace FlexQuery.NET.Tests.Parsers.Fql;

public class FqlAggregateParserTests
{
    private static List<Aggregate> Parse(string? raw) =>
        FqlAggregateParser.Parse(raw);

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
        var result = Parse("SUM(Amount)");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[0].Field.Should().Be("Amount");
        result[0].Alias.Should().Be("AmountSum");
    }

    [Fact]
    public void Parse_SingleAggregate_WithAlias_UsesExplicitAlias()
    {
        var result = Parse("SUM(Amount) AS TotalSales");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[0].Field.Should().Be("Amount");
        result[0].Alias.Should().Be("TotalSales");
    }

    [Fact]
    public void Parse_SingleAggregate_Count_NoAlias_GeneratesDefaultAlias()
    {
        var result = Parse("COUNT(Orders)");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Count);
        result[0].Field.Should().Be("Orders");
        result[0].Alias.Should().Be("OrdersCount");
    }

    [Fact]
    public void Parse_SingleAggregate_Count_WithAlias()
    {
        var result = Parse("COUNT(Orders) AS OrderCount");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Count);
        result[0].Field.Should().Be("Orders");
        result[0].Alias.Should().Be("OrderCount");
    }

    [Fact]
    public void Parse_SingleAggregate_AvgFunction_NoAlias()
    {
        var result = Parse("AVG(Price)");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Avg);
        result[0].Field.Should().Be("Price");
        result[0].Alias.Should().Be("PriceAvg");
    }

    [Fact]
    public void Parse_SingleAggregate_MinFunction_NoAlias()
    {
        var result = Parse("MIN(Date)");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Min);
        result[0].Field.Should().Be("Date");
        result[0].Alias.Should().Be("DateMin");
    }

    [Fact]
    public void Parse_SingleAggregate_MaxFunction_NoAlias()
    {
        var result = Parse("MAX(Date)");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Max);
        result[0].Field.Should().Be("Date");
        result[0].Alias.Should().Be("DateMax");
    }

    [Fact]
    public void Parse_MultipleAggregates_ParsedCorrectly()
    {
        var result = Parse("SUM(Amount), AVG(Price), COUNT(Orders) AS OrderCount");

        result.Should().HaveCount(3);
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[0].Field.Should().Be("Amount");
        result[0].Alias.Should().Be("AmountSum");
        result[1].Function.Should().Be(AggregateFunction.Avg);
        result[1].Field.Should().Be("Price");
        result[1].Alias.Should().Be("PriceAvg");
        result[2].Function.Should().Be(AggregateFunction.Count);
        result[2].Field.Should().Be("Orders");
        result[2].Alias.Should().Be("OrderCount");
    }

    [Fact]
    public void Parse_MultipleAggregates_WithMixedAliases()
    {
        var result = Parse("SUM(Amount) AS Total, AVG(Price), COUNT(Orders) AS OrderCount");

        result.Should().HaveCount(3);
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[0].Field.Should().Be("Amount");
        result[0].Alias.Should().Be("Total");
        result[1].Function.Should().Be(AggregateFunction.Avg);
        result[1].Field.Should().Be("Price");
        result[1].Alias.Should().Be("PriceAvg");
        result[2].Function.Should().Be(AggregateFunction.Count);
        result[2].Field.Should().Be("Orders");
        result[2].Alias.Should().Be("OrderCount");
    }

    [Fact]
    public void Parse_NestedField_GeneratesCorrectAlias()
    {
        var result = Parse("SUM(Orders.Total)");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[0].Field.Should().Be("Orders");
        result[0].Alias.Should().Be("OrdersTotalSum");
    }

    [Fact]
    public void Parse_DeepNestedField_GeneratesCorrectAlias()
    {
        var result = Parse("AVG(Customer.Region.Sales.Price)");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Avg);
        result[0].Field.Should().Be("Customer.Region.Sales");
        result[0].Alias.Should().Be("CustomerRegionSalesPriceAvg");
    }

    [Fact]
    public void Parse_CountCollection_SetsFieldCorrectly()
    {
        var result = Parse("COUNT(Orders)");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Count);
        result[0].Field.Should().Be("Orders");
        result[0].Alias.Should().Be("OrdersCount");
    }

    [Fact]
    public void Parse_FunctionNames_CaseInsensitive()
    {
        var result = Parse("sum(Amount), COUNT(Id), Avg(Price)");

        result.Should().HaveCount(3);
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[1].Function.Should().Be(AggregateFunction.Count);
        result[2].Function.Should().Be(AggregateFunction.Avg);
    }

    [Fact]
    public void Parse_AsKeyword_CaseInsensitive()
    {
        var result = Parse("SUM(Amount) as TotalSales, AVG(Price) AS PriceAvg");

        result.Should().HaveCount(2);
        result[0].Alias.Should().Be("TotalSales");
        result[1].Alias.Should().Be("PriceAvg");
    }

    [Fact]
    public void Parse_Alias_ValidIdentifier_Accepted()
    {
        var result = Parse("SUM(Amount) AS TotalSales");

        result.Should().ContainSingle();
        result[0].Alias.Should().Be("TotalSales");
    }

    [Fact]
    public void Parse_Alias_SameAsField_Accepted()
    {
        var result = Parse("SUM(Amount) AS Amount");

        result.Should().ContainSingle();
        result[0].Alias.Should().Be("Amount");
    }

    [Fact]
    public void Parse_Alias_StartsWithUnderscore_ThrowsFqlParseException()
    {
        var ex = Record.Exception(() => Parse("SUM(Amount) AS _Total"));

        ex.Should().BeOfType<FqlParseException>();
        ex.Message.Should().Contain("Invalid alias");
    }

    [Fact]
    public void Parse_Alias_StartsWithDigit_ThrowsFqlParseException()
    {
        var ex = Record.Exception(() => Parse("SUM(Amount) AS 1Total"));

        ex.Should().BeOfType<FqlParseException>();
        ex.Message.Should().Contain("Invalid alias");
    }

    [Fact]
    public void Parse_Alias_ContainsSpace_ThrowsFqlParseException()
    {
        var ex = Record.Exception(() => Parse("SUM(Amount) AS Total Sales"));

        ex.Should().BeOfType<FqlParseException>();
        ex.Message.Should().Contain("Invalid alias");
    }

    [Fact]
    public void Parse_Alias_ContainsHyphen_ThrowsFqlParseException()
    {
        var ex = Record.Exception(() => Parse("SUM(Amount) AS Total-Sales"));

        ex.Should().BeOfType<FqlParseException>();
        ex.Message.Should().Contain("Invalid alias");
    }

    [Fact]
    public void Parse_Alias_ReservedKeyword_AcceptedAtParserLevel()
    {
        var result = Parse("SUM(Amount) AS SELECT");

        result.Should().ContainSingle();
        result[0].Alias.Should().Be("SELECT");
    }

    [Fact]
    public void Parse_Alias_WhitespaceAroundAs_Handled()
    {
        var result = Parse("SUM(Amount)  AS  TotalSales");

        result.Should().ContainSingle();
        result[0].Alias.Should().Be("TotalSales");
    }

    [Fact]
    public void Parse_MissingOpeningParen_ThrowsFqlParseException()
    {
        var ex = Record.Exception(() => Parse("SUM Amount"));

        ex.Should().BeOfType<FqlParseException>();
        ex.Message.Should().Contain("Missing opening parenthesis");
    }

    [Fact]
    public void Parse_MissingClosingParen_ThrowsFqlParseException()
    {
        var ex = Record.Exception(() => Parse("SUM(Amount"));

        ex.Should().BeOfType<FqlParseException>();
        ex.Message.Should().Contain("Missing closing parenthesis");
    }

    [Fact]
    public void Parse_MissingField_ThrowsFqlParseException()
    {
        var ex = Record.Exception(() => Parse("SUM()"));

        ex.Should().BeOfType<FqlParseException>();
        ex.Message.Should().Contain("Missing field");
    }

    [Fact]
    public void Parse_EmptyAliasAfterAs_ThrowsFqlParseException()
    {
        var ex = Record.Exception(() => Parse("SUM(Amount) AS"));

        ex.Should().BeOfType<FqlParseException>();
        ex.Message.Should().Contain("Missing alias after AS");
    }

    [Fact]
    public void Parse_UnexpectedContentAfterField_ThrowsFqlParseException()
    {
        var ex = Record.Exception(() => Parse("SUM(Amount) EXTRA"));

        ex.Should().BeOfType<FqlParseException>();
        ex.Message.Should().Contain("Unexpected content after field");
    }

    [Fact]
    public void Parse_ExtraClosingParen_ThrowsFqlParseException()
    {
        var ex = Record.Exception(() => Parse("SUM(Amount))"));

        ex.Should().BeOfType<FqlParseException>();
        ex.Message.Should().Contain("Unexpected content after field");
    }

    [Fact]
    public void Parse_UnknownFunction_ThrowsFqlParseException()
    {
        var ex = Record.Exception(() => Parse("UNKNOWN(Amount)"));

        ex.Should().BeOfType<FqlParseException>();
        ex.Message.Should().Contain("Unrecognized function");
    }

    [Fact]
    public void Parse_CountStar_ThrowsFqlParseException()
    {
        var ex = Record.Exception(() => Parse("COUNT(*)"));

        ex.Should().BeOfType<FqlParseException>();
        ex.Message.Should().Contain("COUNT(*) is not supported");
    }

    [Fact]
    public void Parse_LeadingComma_Ignored()
    {
        var result = Parse(",SUM(Amount)");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Sum);
    }

    [Fact]
    public void Parse_TrailingComma_Ignored()
    {
        var result = Parse("SUM(Amount),");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Sum);
    }

    [Fact]
    public void Parse_EmptyItemBetweenCommas_Ignored()
    {
        var result = Parse("SUM(Amount),,AVG(Price)");

        result.Should().HaveCount(2);
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[1].Function.Should().Be(AggregateFunction.Avg);
    }

    [Fact]
    public void Parse_ExtraWhitespaceAroundParts_Handled()
    {
        var result = Parse("  SUM ( Amount ) AS TotalSales  ");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[0].Field.Should().Be("Amount");
        result[0].Alias.Should().Be("TotalSales");
    }

    [Fact]
    public void Parse_MissingAsWithWhitespace_ThrowsFqlParseException()
    {
        var ex = Record.Exception(() => Parse("SUM(Amount) AS  "));

        ex.Should().BeOfType<FqlParseException>();
        ex.Message.Should().Contain("Missing alias after AS");
    }

    [Fact]
    public void Parse_EmptyField_WithWhitespace_ThrowsFqlParseException()
    {
        var ex = Record.Exception(() => Parse("SUM(   )"));

        ex.Should().BeOfType<FqlParseException>();
        ex.Message.Should().Contain("Missing field");
    }

    [Fact]
    public void Parse_Count_WithCollectionPath_Accepted()
    {
        var result = Parse("COUNT(Customer.Orders)");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Count);
        result[0].Field.Should().Be("Customer.Orders");
        result[0].Alias.Should().Be("CustomerOrdersCount");
    }

    [Fact]
    public void Parse_NestedField_WithAlias_ParsedCorrectly()
    {
        var result = Parse("SUM(Orders.Total) AS TotalSales");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[0].Field.Should().Be("Orders");
        result[0].Alias.Should().Be("TotalSales");
    }

    [Fact]
    public void Parse_FunctionOrder_Preserved()
    {
        var result = Parse("SUM(Amount), AVG(Price)");

        result.Should().HaveCount(2);
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[1].Function.Should().Be(AggregateFunction.Avg);
    }

    [Fact]
    public void Parse_Field_StartingWithDot_ThrowsFqlParseException()
    {
        var ex = Record.Exception(() => Parse("SUM(.Orders.Total)"));

        ex.Should().BeOfType<FqlParseException>();
        ex.Message.Should().Contain("Invalid field");
    }

    [Fact]
    public void Parse_Field_EndingWithDot_ThrowsFqlParseException()
    {
        var ex = Record.Exception(() => Parse("SUM(Orders.Total.)"));

        ex.Should().BeOfType<FqlParseException>();
        ex.Message.Should().Contain("Invalid field");
    }

    [Fact]
    public void Parse_Field_DoubleDot_ThrowsFqlParseException()
    {
        var ex = Record.Exception(() => Parse("SUM(Orders..Total)"));

        ex.Should().BeOfType<FqlParseException>();
        ex.Message.Should().Contain("Invalid field");
    }
    
    [Fact]
    public void Parse_Field_CountStar_ThrowsFqlParseException()
    {
        var ex = Record.Exception(() => Parse("COUNT(*)"));

        ex.Should().BeOfType<FqlParseException>();
    }

    [Fact]
    public void Parse_AsWithoutSpaceBeforeAlias_ThrowsFqlParseException()
    {
        var ex = Record.Exception(() => Parse("SUM(Amount)ASTotalSales"));

        ex.Should().BeOfType<FqlParseException>();
        ex.Message.Should().Contain("Unexpected content after field");
    }

    [Fact]
    public void Parse_AutoAlias_CasingPreserved()
    {
        var result = Parse("SUM(Amount)");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[0].Field.Should().Be("Amount");
        result[0].Alias.Should().Be("AmountSum");
    }

    [Fact]
    public void Parse_EmptySegmentsAroundSingleAggregate_Ignored()
    {
        var result = Parse(",,SUM(Amount),,");

        result.Should().ContainSingle();
        result[0].Function.Should().Be(AggregateFunction.Sum);
        result[0].Field.Should().Be("Amount");
        result[0].Alias.Should().Be("AmountSum");
    }
}
