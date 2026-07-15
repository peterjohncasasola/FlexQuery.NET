using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers.Fql;
using Xunit;

namespace FlexQuery.NET.Tests.Parsers.Fql;

public class FqlSelectParserTests
{
    [Fact]
    public void Parse_PropertyPath_Valid()
    {
        var options = new QueryOptions();

        FqlSelectParser.Parse(options, "Id,Name,Profile.AvatarUrl");

        options.Select.Should().BeEquivalentTo(new[] { "Id", "Name", "Profile.AvatarUrl" });
    }

    [Fact]
    public void Parse_AsAlias_Valid()
    {
        var options = new QueryOptions();

        FqlSelectParser.Parse(options, "Name AS FullName");

        options.Select.Should().Contain("Name AS FullName");
    }

    [Fact]
    public void Parse_AggregateExpression_WithParentheses_Valid()
    {
        var options = new QueryOptions();

        FqlSelectParser.Parse(options, "sum(Total) AS TotalSum");

        options.Select.Should().Contain("sum(Total) AS TotalSum");
    }

    [Fact]
    public void Parse_ColonAlias_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "Name:FullName");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_InvalidPropertyPath_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "Name.");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_EmptyString_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "   ");

        act.Should().Throw<FqlParseException>();
    }
}
