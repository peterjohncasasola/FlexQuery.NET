using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
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

        options.Select.Should().BeEquivalentTo([new SelectNode { Field = "Id" }, new SelectNode { Field = "Name" }, new SelectNode { Field = "Profile.AvatarUrl" }]);
    }

    [Fact]
    public void Parse_AsAlias_Simple_Valid()
    {
        var options = new QueryOptions();

        FqlSelectParser.Parse(options, "Name AS FullName");

        options.Select.Should().BeEquivalentTo([new SelectNode { Field = "Name", Alias = "FullName" }]);
    }

    [Fact]
    public void Parse_AsAlias_NavigationPath_Valid()
    {
        var options = new QueryOptions();

        FqlSelectParser.Parse(options, "Customer.Name AS CustomerName");

        options.Select.Should().BeEquivalentTo([new SelectNode { Field = "Customer.Name", Alias = "CustomerName" }]);
    }

    [Fact]
    public void Parse_MixedSelection_Valid()
    {
        var options = new QueryOptions();

        FqlSelectParser.Parse(options, "Id,Name AS FullName,Age,Customer.Name AS CustomerName");

        options.Select.Should().BeEquivalentTo([new SelectNode { Field = "Id" }, new SelectNode { Field = "Name", Alias = "FullName" }, new SelectNode { Field = "Age" }, new SelectNode { Field = "Customer.Name", Alias = "CustomerName" }]);
    }

    [Fact]
    public void Parse_ColonAlias_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "Name:FullName");

        act.Should().Throw<FlexQueryException>();
    }

    [Fact]
    public void Parse_InvalidPropertyPath_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "Name.");

        act.Should().Throw<FlexQueryException>();
    }

    [Fact]
    public void Parse_EmptyString_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "   ");

        act.Should().Throw<FlexQueryException>();
    }

    [Fact]
    public void Parse_InvalidAlias_Empty_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "Name AS ");

        act.Should().Throw<FlexQueryException>();
    }

    [Fact]
    public void Parse_InvalidAlias_StartsWithDigit_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "Name AS 123Alias");

        act.Should().Throw<FlexQueryException>();
    }

    [Fact]
    public void Parse_InvalidAlias_ContainsSpace_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "Name AS Full Name");

        act.Should().Throw<FlexQueryException>();
    }

    [Fact]
    public void Parse_InvalidAlias_ContainsDot_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "Name AS Full.Name");

        act.Should().Throw<FlexQueryException>();
    }

    [Fact]
    public void Parse_AliasReservedKeywordAs_NotThrows()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "Name AS AS");

        act.Should().NotThrow();
        options.Select.Should().BeEquivalentTo([new SelectNode { Field = "Name", Alias = "AS" }]);
    }

    [Fact]
    public void Parse_AliasStartsWithUnderscore_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "Name AS _FullName");

        act.Should().Throw<FlexQueryException>();
    }

    [Fact]
    public void Parse_PropertyPath_StartsWithUnderscore_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "_FullName");

        act.Should().Throw<FlexQueryException>();
    }

    [Fact]
    public void Parse_PropertyPath_StartsWithDigit_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "1Name");

        act.Should().Throw<FlexQueryException>();
    }

    [Fact]
    public void Parse_PropertyPath_SegmentStartsWithUnderscore_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "Customer._Name");

        act.Should().Throw<FlexQueryException>();
    }

    [Fact]
    public void Parse_PropertyPath_SegmentStartsWithDigit_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "Customer.1Name");

        act.Should().Throw<FlexQueryException>();
    }

    [Fact]
    public void Parse_PropertyPath_DoubleDot_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "Customer..Name");

        act.Should().Throw<FlexQueryException>();
    }

    [Fact]
    public void Parse_PropertyPath_LeadingDot_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, ".Customer");

        act.Should().Throw<FlexQueryException>();
    }

    [Fact]
    public void Parse_PropertyPath_TrailingDot_Throws()
    {
        var options = new QueryOptions();

        var act = () => FqlSelectParser.Parse(options, "Customer.");

        act.Should().Throw<FlexQueryException>();
    }

    [Fact]
    public void Parse_WhitespaceAroundAs_Valid()
    {
        var options = new QueryOptions();

        FqlSelectParser.Parse(options, "Name  AS  FullName");

        options.Select.Should().BeEquivalentTo([new SelectNode { Field = "Name", Alias = "FullName" }]);
    }

    [Fact]
    public void Parse_UnderscoreAsSpaceInAlias_Valid()
    {
        var options = new QueryOptions();

        FqlSelectParser.Parse(options, "Name  AS  Full_Name");

        options.Select.Should().BeEquivalentTo([new SelectNode { Field = "Name", Alias = "Full_Name" }]);
    }

    [Fact]
    public void Parse_AliasSameAsField_Valid()
    {
        var options = new QueryOptions();

        FqlSelectParser.Parse(options, "Name AS Name");

        options.Select.Should().BeEquivalentTo([new SelectNode { Field = "Name", Alias = "Name" }]);
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

        act.Should().Throw<FlexQueryException>();
    }

    [Fact]
    public void ParseToSelectionTree_NestedField_StartsWithUnderscore_Throws()
    {
        var act = () => FqlSelectParser.ParseToSelectionTree("Customer(_Name)");

        act.Should().Throw<FlexQueryException>();
    }

    [Fact]
    public void ParseToSelectionTree_NestedField_StartsWithDigit_Throws()
    {
        var act = () => FqlSelectParser.ParseToSelectionTree("Customer(1Name)");

        act.Should().Throw<FlexQueryException>();
    }

    [Fact]
    public void ParseToSelectionTree_NestedField_ContainsHyphen_Throws()
    {
        var act = () => FqlSelectParser.ParseToSelectionTree("Customer(Full-Name)");

        act.Should().Throw<FlexQueryException>();
    }
    
    [Theory]
    [InlineData("Customer(1Name)")]
    [InlineData("Customer(_Name)")]
    [InlineData("Customer(Full Name)")]
    [InlineData("Customer(Full-Name)")]
    public void ParseToSelectionTree_InvalidNestedIdentifier_Throws(string select)
    {
        Action act = () => FqlSelectParser.ParseToSelectionTree(select);

        act.Should().Throw<FlexQueryException>();
    }

    [Fact]
    public void Parse_NestedSyntax_PopulatesSelect()
    {
        var options = new QueryOptions();
        FqlSelectParser.Parse(options, "Customer(Id,Name)");

        options.Select.Should().NotBeNull();
        options.Select!.Count.Should().Be(1);
        options.Select[0].Field.Should().Be("Customer");
        options.Select[0].Children.Count.Should().Be(2);
        options.Select[0].Children[0].Field.Should().Be("Id");
        options.Select[0].Children[1].Field.Should().Be("Name");
        options.SelectTree.Should().BeNull();
    }

    [Fact]
    public void Parse_RootWildcard_PopulatesSelect()
    {
        var options = new QueryOptions();
        FqlSelectParser.Parse(options, "*");

        options.Select.Should().NotBeNull();
        options.Select![0].Field.Should().Be("*");
        options.SelectTree.Should().BeNull();
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
    public void ParseToSelectionTree_NestedAlias_Invalid_ReservedAs_NotThrows()
    {
        var options = new QueryOptions();
        FqlSelectParser.Parse( options,"Customer(Name AS AS)");
        
        options.Select.Should().NotBeNull();
        options.Select![0].Field.Should().Be("Customer");
        options.Select[0].Children.Count.Should().Be(1);
        options.Select[0].Children[0].Field.Should().Be("Name");
        options.Select[0].Children[0].Alias.Should().Be("AS");
        options.SelectTree.Should().BeNull();
    }

    [Fact]
    public void ParseToSelectionTree_NestedAlias_Invalid_NavigationAlias_Throws()
    {
        var act = () => FqlSelectParser.ParseToSelectionTree("Customer AS Client(Name)");

        act.Should().Throw<FqlParseException>()
            .WithMessage("*Navigation alias is not supported*");
    }

    [Fact]
    public void Parse_NestedAlias_RoutesToSelect()
    {
        var options = new QueryOptions();
        FqlSelectParser.Parse(options, "Customer(Name AS CustomerName)");

        options.Select.Should().NotBeNull();
        options.Select![0].Field.Should().Be("Customer");
        options.Select[0].Children.Count.Should().Be(1);
        options.Select[0].Children[0].Field.Should().Be("Name");
        options.Select[0].Children[0].Alias.Should().Be("CustomerName");
        options.SelectTree.Should().BeNull();
    }

    #endregion

    #endregion
}
