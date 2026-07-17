using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Tests.Parsers;

public class GroupByParserTests
{
    [Theory]
    [InlineData("1Customer")]
    [InlineData("_Customer")]
    [InlineData("Customer.1stName")]
    [InlineData("Customer._FirstName")]
    [InlineData("Customer.First Name")]
    [InlineData("Customer.First-Name")]
    [InlineData("Customer..FirstName")]
    [InlineData(".Customer")]
    [InlineData("Customer.")]
    [InlineData("Customer:LastName")]
    public void Parse_InvalidGroupBy_Throws(string groupBy)
    {
        var act = () => GroupByParser.Parse(groupBy);
        act.Should().Throw<FlexQueryParseException>();
    }
    
    [Theory]
    [InlineData("Customer.First_Name")]
    [InlineData("Customer.FirstName")]
    [InlineData("CustomerName")]
    [InlineData("Customer.Address.City")]
    public void Parse_InvalidGroupBy_NotThrows(string groupBy)
    {
        var act = () => GroupByParser.Parse(groupBy);
        act.Should().NotThrow();
        act.Should().NotBeNull();
    }
}