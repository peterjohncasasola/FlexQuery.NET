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
    public void Parse_AsAlias_Simple_Valid()
    {
        var options = new QueryOptions();

        FqlSelectParser.Parse(options, "Name AS FullName");

        options.Select.Should().BeEquivalentTo(new[] { "Name AS FullName" });
    }

    [Fact]
    public void Parse_AsAlias_NavigationPath_Valid()
    {
        var options = new QueryOptions();

        FqlSelectParser.Parse(options, "Customer.Name AS CustomerName");

        options.Select.Should().BeEquivalentTo(new[] { "Customer.Name AS CustomerName" });
    }

    [Fact]
    public void Parse_MixedSelection_Valid()
    {
        var options = new QueryOptions();

        FqlSelectParser.Parse(options, "Id,Name AS FullName,Age,Customer.Name AS CustomerName");

        options.Select.Should().BeEquivalentTo(new[] { "Id", "Name AS FullName", "Age", "Customer.Name AS CustomerName" });
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

    [Fact]
    public void Parse_InvalidAlias_Empty_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "Name AS ");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_InvalidAlias_StartsWithDigit_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "Name AS 123Alias");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_InvalidAlias_ContainsSpace_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "Name AS Full Name");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_InvalidAlias_ContainsDot_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "Name AS Full.Name");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_AliasReservedKeywordAs_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "Name AS AS");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_AliasStartsWithUnderscore_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "Name AS _FullName");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_WhitespaceAroundAs_Valid()
    {
        var options = new QueryOptions();

        FqlSelectParser.Parse(options, "Name  AS  FullName");

        options.Select.Should().BeEquivalentTo(new[] { "Name AS FullName" });
    }

    [Fact]
    public void Parse_UnderscoreAsSpaceInAlias_Valid()
    {
        var options = new QueryOptions();

        FqlSelectParser.Parse(options, "Name  AS  Full_Name");

        options.Select.Should().BeEquivalentTo(new[] { "Name AS Full_Name" });
    }

    [Fact]
    public void Parse_AliasSameAsField_Valid()
    {
        var options = new QueryOptions();

        FqlSelectParser.Parse(options, "Name AS Name");

        options.Select.Should().BeEquivalentTo(new[] { "Name AS Name" });
    }
}
