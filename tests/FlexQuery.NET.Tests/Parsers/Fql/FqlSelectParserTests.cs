using FlexQuery.NET.Internal;
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

    #region Nested Select

    [Fact]
    public void ParseToSelectionTree_SimpleFields_ReturnsFlatTree()
    {
        var tree = FqlSelectParser.ParseToSelectionTree("Id,Name");

        tree.Should().NotBeNull();
        tree!.TryGetChild("Id", out _).Should().BeTrue();
        tree.TryGetChild("Name", out _).Should().BeTrue();
        tree.HasChildren.Should().BeTrue();
    }

    [Fact]
    public void ParseToSelectionTree_NestedReference_ReturnsNestedTree()
    {
        var tree = FqlSelectParser.ParseToSelectionTree("Customer(Id,Name)");

        tree.Should().NotBeNull();
        tree!.TryGetChild("Customer", out var customer).Should().BeTrue();
        customer!.TryGetChild("Id", out _).Should().BeTrue();
        customer.TryGetChild("Name", out _).Should().BeTrue();
    }

    [Fact]
    public void ParseToSelectionTree_CollectionNavigation_ReturnsNestedTree()
    {
        var tree = FqlSelectParser.ParseToSelectionTree("Orders(Id,Total)");

        tree.Should().NotBeNull();
        tree!.TryGetChild("Orders", out var orders).Should().BeTrue();
        orders!.TryGetChild("Id", out _).Should().BeTrue();
        orders.TryGetChild("Total", out _).Should().BeTrue();
    }

    [Fact]
    public void ParseToSelectionTree_DeeplyNested_ReturnsRecursiveTree()
    {
        var tree = FqlSelectParser.ParseToSelectionTree("Customer(Address(City,Country))");

        tree.Should().NotBeNull();
        tree!.TryGetChild("Customer", out var customer).Should().BeTrue();
        customer!.TryGetChild("Address", out var address).Should().BeTrue();
        address!.TryGetChild("City", out _).Should().BeTrue();
        address.TryGetChild("Country", out _).Should().BeTrue();
    }

    [Fact]
    public void ParseToSelectionTree_RootWildcard_SetsIncludeAllScalars()
    {
        var tree = FqlSelectParser.ParseToSelectionTree("*");

        tree.Should().NotBeNull();
        tree!.IncludeAllScalars.Should().BeTrue();
        tree.HasChildren.Should().BeFalse();
    }

    [Fact]
    public void ParseToSelectionTree_DuplicateNodes_Merges()
    {
        var tree = FqlSelectParser.ParseToSelectionTree("Customer(Id),Customer(Name)");

        tree.Should().NotBeNull();
        tree!.TryGetChild("Customer", out var customer).Should().BeTrue();
        customer!.TryGetChild("Id", out _).Should().BeTrue();
        customer.TryGetChild("Name", out _).Should().BeTrue();
    }

    [Fact]
    public void ParseToSelectionTree_WhitespaceVariations_ParsesCorrectly()
    {
        var tree = FqlSelectParser.ParseToSelectionTree("Customer ( Id , Name )");

        tree.Should().NotBeNull();
        tree!.TryGetChild("Customer", out var customer).Should().BeTrue();
        customer!.TryGetChild("Id", out _).Should().BeTrue();
        customer.TryGetChild("Name", out _).Should().BeTrue();
    }

    [Fact]
    public void ParseToSelectionTree_Newlines_ParsesCorrectly()
    {
        var tree = FqlSelectParser.ParseToSelectionTree("Customer(\n  Id,\n  Name\n)");

        tree.Should().NotBeNull();
        tree!.TryGetChild("Customer", out var customer).Should().BeTrue();
        customer!.TryGetChild("Id", out _).Should().BeTrue();
        customer.TryGetChild("Name", out _).Should().BeTrue();
    }

    [Fact]
    public void ParseToSelectionTree_EmptyInput_Throws()
    {
        var act = () => FqlSelectParser.ParseToSelectionTree("   ");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public void ParseToSelectionTree_NestedWildcard_Throws()
    {
        var act = () => FqlSelectParser.ParseToSelectionTree("Customer(*)");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Wildcard selection is only supported at the root level*");
    }

    [Fact]
    public void ParseToSelectionTree_MixedWildcard_Throws()
    {
        var act = () => FqlSelectParser.ParseToSelectionTree("*,Id");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Root wildcard cannot be combined with other selections*");
    }

    [Fact]
    public void ParseToSelectionTree_MultipleRootWildcards_Throws()
    {
        var act = () => FqlSelectParser.ParseToSelectionTree("*,*");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Root wildcard cannot be combined with other selections*");
    }

    [Fact]
    public void ParseToSelectionTree_PropertyWildcard_Throws()
    {
        var act = () => FqlSelectParser.ParseToSelectionTree("Customer.*");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Property wildcard syntax is not supported*");
    }

    [Fact]
    public void ParseToSelectionTree_EmptyChildList_Throws()
    {
        var act = () => FqlSelectParser.ParseToSelectionTree("Customer()");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Empty selection list for 'Customer'*");
    }

    [Fact]
    public void ParseToSelectionTree_MismatchedParens_Throws()
    {
        var act = () => FqlSelectParser.ParseToSelectionTree("Customer(Name))");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Unexpected closing parenthesis*");
    }

    [Fact]
    public void ParseToSelectionTree_MissingClosingParen_Throws()
    {
        var act = () => FqlSelectParser.ParseToSelectionTree("Customer(Name");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Missing closing parenthesis*");
    }

    [Fact]
    public void ParseToSelectionTree_LeadingComma_Throws()
    {
        var act = () => FqlSelectParser.ParseToSelectionTree(",Id,Name");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Expected identifier before comma*");
    }

    [Fact]
    public void ParseToSelectionTree_TrailingComma_Throws()
    {
        var act = () => FqlSelectParser.ParseToSelectionTree("Id,Name,");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Trailing comma is not allowed*");
    }

    [Fact]
    public void ParseToSelectionTree_ExtraClosingParen_Throws()
    {
        var act = () => FqlSelectParser.ParseToSelectionTree("Customer(Name))");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Unexpected closing parenthesis*");
    }

    [Fact]
    public void ParseToSelectionTree_InvalidIdentifier_Throws()
    {
        var act = () => FqlSelectParser.ParseToSelectionTree("Customer(Id-Name)");

        act.Should().Throw<FqlParseException>();
    }

    [Fact]
    public void Parse_NestedSyntax_PopulatesSelectTree()
    {
        var options = new QueryOptions();
        FqlSelectParser.Parse(options, "Customer(Id,Name)");

        options.Select.Should().BeNull();
        options.SelectTree.Should().NotBeNull();
        options.SelectTree!.TryGetChild("Customer", out var customer).Should().BeTrue();
        customer!.TryGetChild("Id", out _).Should().BeTrue();
        customer.TryGetChild("Name", out _).Should().BeTrue();
    }

    [Fact]
    public void Parse_RootWildcard_PopulatesSelectTree()
    {
        var options = new QueryOptions();
        FqlSelectParser.Parse(options, "*");

        options.Select.Should().BeNull();
        options.SelectTree.Should().NotBeNull();
        options.SelectTree!.IncludeAllScalars.Should().BeTrue();
    }

    #region Nested Alias

    [Fact]
    public void ParseToSelectionTree_NestedAlias_SetsAliasOnChild()
    {
        var tree = FqlSelectParser.ParseToSelectionTree("Customer(Name AS CustomerName)");

        tree.Should().NotBeNull();
        tree!.TryGetChild("Customer", out var customer).Should().BeTrue();
        customer!.TryGetChild("Name", out var name).Should().BeTrue();
        name!.Alias.Should().Be("CustomerName");
    }

    [Fact]
    public void ParseToSelectionTree_NestedAlias_MultipleAliases()
    {
        var tree = FqlSelectParser.ParseToSelectionTree("Customer(Name AS CustomerName,Email AS CustomerEmail)");

        tree.Should().NotBeNull();
        tree!.TryGetChild("Customer", out var customer).Should().BeTrue();
        customer!.TryGetChild("Name", out var name).Should().BeTrue();
        name!.Alias.Should().Be("CustomerName");
        customer.TryGetChild("Email", out var email).Should().BeTrue();
        email!.Alias.Should().Be("CustomerEmail");
    }

    [Fact]
    public void ParseToSelectionTree_NestedAlias_DeeplyNested()
    {
        var tree = FqlSelectParser.ParseToSelectionTree("Customer(Address(City AS CustomerCity))");

        tree.Should().NotBeNull();
        tree!.TryGetChild("Customer", out var customer).Should().BeTrue();
        customer!.TryGetChild("Address", out var address).Should().BeTrue();
        address!.TryGetChild("City", out var city).Should().BeTrue();
        city!.Alias.Should().Be("CustomerCity");
    }

    [Fact]
    public void ParseToSelectionTree_NestedAlias_Invalid_EmptyAlias_Throws()
    {
        var act = () => FqlSelectParser.ParseToSelectionTree("Customer(Name AS )");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Empty alias*");
    }

    [Fact]
    public void ParseToSelectionTree_NestedAlias_Invalid_ReservedAs_Throws()
    {
        var act = () => FqlSelectParser.ParseToSelectionTree("Customer(Name AS AS)");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*reserved keyword*");
    }

    [Fact]
    public void ParseToSelectionTree_NestedAlias_Invalid_NavigationAlias_Throws()
    {
        var act = () => FqlSelectParser.ParseToSelectionTree("Customer AS Client(Name)");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Navigation alias is not supported*");
    }

    [Fact]
    public void Parse_NestedAlias_RoutesToSelectTree()
    {
        var options = new QueryOptions();
        FqlSelectParser.Parse(options, "Customer(Name AS CustomerName)");

        options.Select.Should().BeNull();
        options.SelectTree.Should().NotBeNull();
        options.SelectTree!.TryGetChild("Customer", out var customer).Should().BeTrue();
        customer!.TryGetChild("Name", out var name).Should().BeTrue();
        name!.Alias.Should().Be("CustomerName");
    }

    #endregion

    #endregion
}
