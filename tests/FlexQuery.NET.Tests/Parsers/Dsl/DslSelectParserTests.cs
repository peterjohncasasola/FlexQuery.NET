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
    public void Parse_SimpleAlias_PopulatesSelect()
    {
        var options = new QueryOptions();

        DslSelectParser.Parse(options, "DateOfBirth:BirthDate");

        options.Select.Should().Contain("DateOfBirth AS BirthDate");
    }

    [Fact]
    public void Parse_NavigationAlias_PopulatesSelect()
    {
        var options = new QueryOptions();

        DslSelectParser.Parse(options, "Customer.Name:CustomerName");

        options.Select.Should().Contain("Customer.Name AS CustomerName");
    }

    [Fact]
    public void Parse_InvalidAlias_Empty_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Name:");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_InvalidAlias_StartsWithDigit_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Name:123Alias");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_InvalidAlias_ContainsSpace_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Name:Customer Name");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_EmptyAlias_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, ":Alias");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_EmptyPath_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, ":FullName");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_MultipleColons_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Name:First:Last");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_AliasReservedKeywordAs_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Name:AS");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_AliasReservedKeywordAsCaseInsensitive_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Name:as");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_AliasWithDot_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Name:Customer.Name");

        act.Should().Throw<DslParseException>();
    }
    
    [Fact]
    public void Parse_AliasStartsWithUnderscore_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Name:_FullName");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_WhitespaceAroundColon_Valid()
    {
        var options = new QueryOptions();

        DslSelectParser.Parse(options, "Name : FullName");

        options.Select.Should().Contain("Name AS FullName");
    }

    [Fact]
    public void Parse_PlainPropertyPath_Valid()
    {
        var options = new QueryOptions();

        DslSelectParser.Parse(options, "Name,Customer.Name");

        options.Select.Should().BeEquivalentTo(new[] { "Name", "Customer.Name" });
    }

    [Fact]
    public void Parse_AliasSameAsField_Valid()
    {
        var options = new QueryOptions();

        DslSelectParser.Parse(options, "Name:Name");

        options.Select.Should().Contain("Name AS Name");
    }
}
