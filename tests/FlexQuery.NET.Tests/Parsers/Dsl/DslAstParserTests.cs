using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Parsers.Dsl;
using Xunit;

namespace FlexQuery.NET.Tests.Parsers.Dsl;

public class DslAstParserTests
{
    [Theory]
    [InlineData("name:foobar:john")]
    [InlineData("age:xyz:20")]
    [InlineData("name:UNKNOWN:john")]
    public void Parse_UnsupportedOperator_ThrowsDslParseException(string input)
    {
        var act = () => DslAstParser.Parse(input);

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_UnsupportedOperator_ErrorMessageContainsOperator()
    {
        var ex = Record.Exception(() => DslAstParser.Parse("name:foobar:john"));

        ex.Should().BeOfType<DslParseException>();
        ex.Message.Should().Contain("foobar");
        ex.Message.Should().Contain("Unsupported");
    }

    [Theory]
    [InlineData("name:eq:john")]
    [InlineData("age:gt:20")]
    [InlineData("age:gte:20")]
    [InlineData("age:lt:20")]
    [InlineData("age:lte:20")]
    [InlineData("name:neq:john")]
    [InlineData("name:contains:john")]
    [InlineData("name:startswith:jo")]
    [InlineData("name:endswith:hn")]
    [InlineData("name:like:%john%")]
    [InlineData("name:isnull")]
    [InlineData("name:isnotnull")]
    [InlineData("name:in:Active,Pending")]
    public void Parse_SupportedOperators_Succeed(string input)
    {
        var act = () => DslAstParser.Parse(input);

        act.Should().NotThrow();
    }
}
