using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers.Dsl;
using Xunit;

namespace FlexQuery.NET.Tests.Parsers.Dsl;

public class DslSelectParserTests
{
    [Fact]
    public void Parse_EmptyString_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "   ");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_ValidPaths_PopulatesSelect()
    {
        var options = new QueryOptions();

        DslSelectParser.Parse(options, "Id,Name,Profile.AvatarUrl");

        options.Select.Should().BeEquivalentTo(new[] { "Id", "Name", "Profile.AvatarUrl" });
    }

    [Fact]
    public void Parse_AliasExpression_ExtractsPathBeforeAs()
    {
        var options = new QueryOptions();

        DslSelectParser.Parse(options, "Name AS FullName, Age");

        options.Select.Should().Contain("Name AS FullName");
        options.Select.Should().Contain("Age");
    }

    [Fact]
    public void Parse_InvalidPropertyPath_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Name.");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_WhitespaceItems_AreSkipped()
    {
        var options = new QueryOptions();

        DslSelectParser.Parse(options, "Id, , Name");

        options.Select.Should().BeEquivalentTo(new[] { "Id", "Name" });
    }

    [Fact]
    public void Parse_ColonAlias_Simple()
    {
        var options = new QueryOptions();

        DslSelectParser.Parse(options, "Name:FullName");

        options.Select.Should().Contain("Name AS FullName");
    }

    [Fact]
    public void Parse_ColonAlias_NestedPath()
    {
        var options = new QueryOptions();

        DslSelectParser.Parse(options, "Customer.Name:CustomerName");

        options.Select.Should().Contain("Customer.Name AS CustomerName");
    }

    [Fact]
    public void Parse_ColonAlias_EmptyAlias_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Name:");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_ColonAlias_EmptyPath_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, ":FullName");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_ColonAlias_AliasWithSpace_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Name:Full Name");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_ColonAlias_MultipleColons_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Name:First:Last");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_ColonAlias_ReservedKeywordAs_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Name:AS");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_ColonAlias_ReservedKeywordAsCaseInsensitive_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Name:as");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_ColonAlias_MixedWithPlain()
    {
        var options = new QueryOptions();

        DslSelectParser.Parse(options, "Id,Name:FullName,Age");

        options.Select.Should().BeEquivalentTo(new[] { "Id", "Name AS FullName", "Age" });
    }

    [Fact]
    public void Parse_ColonAlias_WhitespaceAroundColon()
    {
        var options = new QueryOptions();

        DslSelectParser.Parse(options, "Name : FullName");

        options.Select.Should().Contain("Name AS FullName");
    }

    [Fact]
    public void Parse_BackwardCompat_AsSyntax_StillWorks()
    {
        var options = new QueryOptions();

        DslSelectParser.Parse(options, "Name AS FullName");

        options.Select.Should().Contain("Name AS FullName");
    }

    [Fact]
    public void Parse_AggregateExpression_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "SUM(Total):Revenue");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_ColonAlias_InvalidIdentifier_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Name:Full-Name");

        act.Should().Throw<DslParseException>();
    }
}
