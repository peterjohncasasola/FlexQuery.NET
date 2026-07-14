using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Dsl;
using Xunit;

namespace FlexQuery.NET.Tests.Parsers;

public class SelectParserTests
{
    [Fact]
    public void Parse_EmptyString_Throws()
    {
        var options = new QueryOptions();

        var act = () => SelectParser.Parse(options, "   ");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_ValidPaths_PopulatesSelect()
    {
        var options = new QueryOptions();

        SelectParser.Parse(options, "Id,Name,Profile.AvatarUrl");

        options.Select.Should().BeEquivalentTo(new[] { "Id", "Name", "Profile.AvatarUrl" });
    }

    [Fact]
    public void Parse_AggregateLookingToken_BypassesPathValidation()
    {
        var options = new QueryOptions();

        SelectParser.Parse(options, "Id,SUM(Total):TotalRevenue");

        options.Select.Should().Contain("Id");
        options.Select.Should().Contain("SUM(Total):TotalRevenue");
    }

    [Fact]
    public void Parse_AliasExpression_ExtractsPathBeforeAs()
    {
        var options = new QueryOptions();

        SelectParser.Parse(options, "Name AS FullName, Age");

        options.Select.Should().Contain("Name AS FullName");
        options.Select.Should().Contain("Age");
    }

    [Fact]
    public void Parse_InvalidPropertyPath_Throws()
    {
        var options = new QueryOptions();

        var act = () => SelectParser.Parse(options, "Name.");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_WhitespaceItems_AreSkipped()
    {
        var options = new QueryOptions();

        SelectParser.Parse(options, "Id, , Name");

        options.Select.Should().BeEquivalentTo(new[] { "Id", "Name" });
    }
}
