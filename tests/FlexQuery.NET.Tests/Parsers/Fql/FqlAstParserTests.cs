using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Parsers.Fql;
using Xunit;

namespace FlexQuery.NET.Tests.Parsers.Fql;

public class FqlAstParserTests
{
    [Theory]
    [InlineData("Name %% 'John'")]
    [InlineData("Name FOO 'John'")]
    [InlineData("Name BAR 'John'")]
    [InlineData("Age ## 20")]
    public void Parse_UnsupportedOperator_ThrowsFqlParseException(string input)
    {
        var act = () => FqlAstParser.Parse(input);

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_UnsupportedOperator_ErrorMessageContainsToken()
    {
        var ex = Record.Exception(() => FqlAstParser.Parse("Name %% 'John'"));

        ex.Should().BeOfType<FqlParseException>();
        ex.Message.Should().Contain("Expected operator");
    }

    [Theory]
    [InlineData("Name = 'John'")]
    [InlineData("Age > 20")]
    [InlineData("Age >= 20")]
    [InlineData("Age < 20")]
    [InlineData("Age <= 20")]
    [InlineData("Age != 20")]
    [InlineData("Age <> 20")]
    [InlineData("Name LIKE '%john%'")]
    [InlineData("Name IS NULL")]
    [InlineData("Name IS NOT NULL")]
    public void Parse_SupportedOperators_Succeed(string input)
    {
        var act = () => FqlAstParser.Parse(input);

        act.Should().NotThrow();
    }
}
