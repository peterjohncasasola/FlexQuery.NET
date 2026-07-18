using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
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

        options.Select.Should().BeEquivalentTo(new[]
        {
            new SelectNode { Field = "Id" },
            new SelectNode { Field = "Name" },
            new SelectNode { Field = "Profile.AvatarUrl" }
        });
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

        options.Select.Should().BeEquivalentTo(new[]
        {
            new SelectNode { Field = "Id" },
            new SelectNode { Field = "Name" }
        });
    }

    [Fact]
    public void Parse_SimpleAlias_PopulatesSelect()
    {
        var options = new QueryOptions();

        DslSelectParser.Parse(options, "DateOfBirth:BirthDate");

        options.Select.Should().ContainEquivalentOf(new SelectNode { Field = "DateOfBirth", Alias = "BirthDate" });
    }

    [Fact]
    public void Parse_NavigationAlias_PopulatesSelect()
    {
        var options = new QueryOptions();

        DslSelectParser.Parse(options, "Customer.Name:CustomerName");

        options.Select.Should().ContainEquivalentOf(new SelectNode { Field = "Customer.Name", Alias = "CustomerName" });
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
    public void Parse_AliasReservedKeywordAs_NotThrows()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Name:AS");

        act.Should().NotThrow();
        options.Select.Should().BeEquivalentTo([new SelectNode { Field = "Name", Alias = "AS" }]);
    }

    [Fact]
    public void Parse_AliasReservedKeywordAsCaseInsensitive_NotThrows()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Name:as");

        act.Should().NotThrow();
        options.Select.Should().BeEquivalentTo([new SelectNode { Field = "Name", Alias = "as" }]);
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
    public void Parse_PropertyPath_StartsWithUnderscore_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "_FullName");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_PropertyPath_StartsWithDigit_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "1Name");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_PropertyPath_SegmentStartsWithUnderscore_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Customer._Name");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_PropertyPath_SegmentStartsWithDigit_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Customer.1Name");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_PropertyPath_DoubleDot_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Customer..Name");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_PropertyPath_LeadingDot_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, ".Customer");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_PropertyPath_TrailingDot_Throws()
    {
        var options = new QueryOptions();

        var act = () => DslSelectParser.Parse(options, "Customer.");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_WhitespaceAroundColon_Valid()
    {
        var options = new QueryOptions();

        DslSelectParser.Parse(options, "Name : FullName");

        options.Select.Should().ContainEquivalentOf(new SelectNode { Field = "Name", Alias = "FullName" });
    }

    [Fact]
    public void Parse_PlainPropertyPath_Valid()
    {
        var options = new QueryOptions();

        DslSelectParser.Parse(options, "Name,Customer.Name");

        options.Select.Should().BeEquivalentTo([new SelectNode { Field = "Name" }, new SelectNode { Field = "Customer.Name" }]);
    }

    [Fact]
    public void Parse_AliasSameAsField_Valid()
    {
        var options = new QueryOptions();

        DslSelectParser.Parse(options, "Name:Name");

        options.Select.Should().ContainEquivalentOf(new SelectNode { Field = "Name", Alias = "Name" });
    }

    #region Nested Select

    [Fact]
    public void ParseToSelectionTree_SimpleFields_ReturnsFlatTree()
    {
        var tree = DslSelectParser.ParseToSelectionTree("Id,Name");

        tree.Should().NotBeNull();
        tree!.TryGetChild("Id", out _).Should().BeTrue();
        tree.TryGetChild("Name", out _).Should().BeTrue();
        tree.HasChildren.Should().BeTrue();
    }

    [Fact]
    public void ParseToSelectionTree_NestedReference_ReturnsNestedTree()
    {
        var tree = DslSelectParser.ParseToSelectionTree("Customer(Id,Name)");

        tree.Should().NotBeNull();
        tree!.TryGetChild("Customer", out var customer).Should().BeTrue();
        customer!.TryGetChild("Id", out _).Should().BeTrue();
        customer.TryGetChild("Name", out _).Should().BeTrue();
    }

    [Fact]
    public void ParseToSelectionTree_CollectionNavigation_ReturnsNestedTree()
    {
        var tree = DslSelectParser.ParseToSelectionTree("Orders(Id,Total)");

        tree.Should().NotBeNull();
        tree!.TryGetChild("Orders", out var orders).Should().BeTrue();
        orders!.TryGetChild("Id", out _).Should().BeTrue();
        orders.TryGetChild("Total", out _).Should().BeTrue();
    }

    [Fact]
    public void ParseToSelectionTree_DeeplyNested_ReturnsRecursiveTree()
    {
        var tree = DslSelectParser.ParseToSelectionTree("Customer(Address(City,Country))");

        tree.Should().NotBeNull();
        tree!.TryGetChild("Customer", out var customer).Should().BeTrue();
        customer!.TryGetChild("Address", out var address).Should().BeTrue();
        address!.TryGetChild("City", out _).Should().BeTrue();
        address.TryGetChild("Country", out _).Should().BeTrue();
    }

    [Fact]
    public void ParseToSelectionTree_RootWildcard_SetsIncludeAllScalars()
    {
        var tree = DslSelectParser.ParseToSelectionTree("*");

        tree.Should().NotBeNull();
        tree!.IncludeAllScalars.Should().BeTrue();
        tree.HasChildren.Should().BeFalse();
    }

    [Fact]
    public void ParseToSelectionTree_DuplicateNodes_Merges()
    {
        var tree = DslSelectParser.ParseToSelectionTree("Customer(Id),Customer(Name)");

        tree.Should().NotBeNull();
        tree!.TryGetChild("Customer", out var customer).Should().BeTrue();
        customer!.TryGetChild("Id", out _).Should().BeTrue();
        customer.TryGetChild("Name", out _).Should().BeTrue();
    }

    [Fact]
    public void ParseToSelectionTree_WhitespaceVariations_ParsesCorrectly()
    {
        var tree = DslSelectParser.ParseToSelectionTree("Customer ( Id , Name )");

        tree.Should().NotBeNull();
        tree!.TryGetChild("Customer", out var customer).Should().BeTrue();
        customer!.TryGetChild("Id", out _).Should().BeTrue();
        customer.TryGetChild("Name", out _).Should().BeTrue();
    }

    [Fact]
    public void ParseToSelectionTree_Newlines_ParsesCorrectly()
    {
        var tree = DslSelectParser.ParseToSelectionTree("Customer(\n  Id,\n  Name\n)");

        tree.Should().NotBeNull();
        tree!.TryGetChild("Customer", out var customer).Should().BeTrue();
        customer!.TryGetChild("Id", out _).Should().BeTrue();
        customer.TryGetChild("Name", out _).Should().BeTrue();
    }

    [Fact]
    public void ParseToSelectionTree_EmptyInput_Throws()
    {
        var act = () => DslSelectParser.ParseToSelectionTree("   ");

        act.Should().Throw<DslParseException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public void ParseToSelectionTree_NestedWildcard_Throws()
    {
        var act = () => DslSelectParser.ParseToSelectionTree("Customer(*)");

        act.Should().Throw<DslParseException>()
            .WithMessage("*Wildcard selection is only supported at the root level*");
    }

    [Fact]
    public void ParseToSelectionTree_MixedWildcard_Throws()
    {
        var act = () => DslSelectParser.ParseToSelectionTree("*,Id");

        act.Should().Throw<DslParseException>()
            .WithMessage("*Root wildcard cannot be combined with other selections*");
    }

    [Fact]
    public void ParseToSelectionTree_MultipleRootWildcards_Throws()
    {
        var act = () => DslSelectParser.ParseToSelectionTree("*,*");

        act.Should().Throw<DslParseException>()
            .WithMessage("*Root wildcard cannot be combined with other selections*");
    }

    [Fact]
    public void ParseToSelectionTree_PropertyWildcard_Throws()
    {
        var act = () => DslSelectParser.ParseToSelectionTree("Customer.*");

        act.Should().Throw<DslParseException>()
            .WithMessage("*Property wildcard syntax is not supported*");
    }

    [Fact]
    public void ParseToSelectionTree_EmptyChildList_Throws()
    {
        var act = () => DslSelectParser.ParseToSelectionTree("Customer()");

        act.Should().Throw<DslParseException>()
            .WithMessage("*Empty selection list for 'Customer'*");
    }

    [Fact]
    public void ParseToSelectionTree_MismatchedParens_Throws()
    {
        var act = () => DslSelectParser.ParseToSelectionTree("Customer(Name))");

        act.Should().Throw<DslParseException>()
            .WithMessage("*Unexpected closing parenthesis*");
    }

    [Fact]
    public void ParseToSelectionTree_MissingClosingParen_Throws()
    {
        var act = () => DslSelectParser.ParseToSelectionTree("Customer(Name");

        act.Should().Throw<DslParseException>()
            .WithMessage("*Missing closing parenthesis*");
    }

    [Fact]
    public void ParseToSelectionTree_LeadingComma_Throws()
    {
        var act = () => DslSelectParser.ParseToSelectionTree(",Id,Name");

        act.Should().Throw<DslParseException>()
            .WithMessage("*Expected identifier before comma*");
    }

    [Fact]
    public void ParseToSelectionTree_TrailingComma_Throws()
    {
        var act = () => DslSelectParser.ParseToSelectionTree("Id,Name,");

        act.Should().Throw<DslParseException>()
            .WithMessage("*Trailing comma is not allowed*");
    }

    [Fact]
    public void ParseToSelectionTree_ExtraClosingParen_Throws()
    {
        var act = () => DslSelectParser.ParseToSelectionTree("Customer(Name))");

        act.Should().Throw<DslParseException>()
            .WithMessage("*Unexpected closing parenthesis*");
    }

    [Fact]
    public void ParseToSelectionTree_InvalidIdentifier_Throws()
    {
        var act = () => DslSelectParser.ParseToSelectionTree("Customer(Id-Name)");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void ParseToSelectionTree_NestedField_StartsWithUnderscore_Throws()
    {
        var act = () => DslSelectParser.ParseToSelectionTree("Customer(_Name)");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void ParseToSelectionTree_NestedField_StartsWithDigit_Throws()
    {
        var act = () => DslSelectParser.ParseToSelectionTree("Customer(1Name)");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void ParseToSelectionTree_NestedField_ContainsHyphen_Throws()
    {
        var act = () => DslSelectParser.ParseToSelectionTree("Customer(Full-Name)");

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void ParseToSelectionTree_NestedField_ContainsSpace_Throws()
    {
        var act = () => DslSelectParser.ParseToSelectionTree("Customer(Full Name)");

        act.Should().Throw<DslParseException>();
    }

    [Theory]
    [InlineData("Customer(1Name)")]
    [InlineData("Customer(_Name)")]
    [InlineData("Customer(Full Name)")]
    [InlineData("Customer(Full-Name)")]
    public void ParseToSelectionTree_InvalidNestedIdentifier_Throws(string select)
    {
        Action act = () => DslSelectParser.ParseToSelectionTree(select);

        act.Should().Throw<DslParseException>();
    }

    [Fact]
    public void Parse_NestedSyntax_PopulatesSelect()
    {
        var options = new QueryOptions();
        DslSelectParser.Parse(options, "Customer(Id,Name)");

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
        DslSelectParser.Parse(options, "*");

        options.Select.Should().NotBeNull();
        options.Select![0].Field.Should().Be("*");
        options.SelectTree.Should().BeNull();
    }

    #region Nested Alias

    [Fact]
    public void ParseToSelectionTree_NestedAlias_SetsAliasOnChild()
    {
        var tree = DslSelectParser.ParseToSelectionTree("Customer(Name:CustomerName)");

        tree.Should().NotBeNull();
        tree!.TryGetChild("Customer", out var customer).Should().BeTrue();
        customer!.TryGetChild("Name", out var name).Should().BeTrue();
        name!.Alias.Should().Be("CustomerName");
    }

    [Fact]
    public void ParseToSelectionTree_NestedAlias_MultipleAliases()
    {
        var tree = DslSelectParser.ParseToSelectionTree("Customer(Name:CustomerName,Email:CustomerEmail)");

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
        var tree = DslSelectParser.ParseToSelectionTree("Customer(Address(City:CustomerCity))");

        tree.Should().NotBeNull();
        tree!.TryGetChild("Customer", out var customer).Should().BeTrue();
        customer!.TryGetChild("Address", out var address).Should().BeTrue();
        address!.TryGetChild("City", out var city).Should().BeTrue();
        city!.Alias.Should().Be("CustomerCity");
    }

    [Fact]
    public void ParseToSelectionTree_NestedAlias_Invalid_EmptyAlias_Throws()
    {
        var act = () => DslSelectParser.ParseToSelectionTree("Customer(Name:)");

        act.Should().Throw<DslParseException>()
            .WithMessage("*Empty alias*");
    }

    [Fact]
    public void ParseToSelectionTree_NestedAlias_Invalid_ReservedAs_Throws()
    {
        var act = () => DslSelectParser.ParseToSelectionTree("Customer(Name:AS)");

        act.Should().Throw<DslParseException>()
            .WithMessage("*reserved keyword*");
    }

    [Fact]
    public void ParseToSelectionTree_NestedAlias_Invalid_NavigationAlias_Throws()
    {
        var act = () => DslSelectParser.ParseToSelectionTree("Customer:Client(Name)");

        act.Should().Throw<DslParseException>()
            .WithMessage("*Navigation alias is not supported*");
    }

    [Fact]
    public void Parse_NestedAlias_RoutesToSelect()
    {
        var options = new QueryOptions();
        DslSelectParser.Parse(options, "Customer(Name:CustomerName)");

        options.Select.Should().NotBeNull();
        options.Select![0].Field.Should().Be("Customer");
        options.Select[0].Children.Count.Should().Be(1);
        options.Select[0].Children[0].Field.Should().Be("Name");
        options.Select[0].Children[0].Alias.Should().Be("CustomerName");
        options.SelectTree.Should().BeNull();
    }

    [Fact]
    public void ParseToSelectionTree_MixedFlatAndNestedSyntax_Valid()
    {
        var tree = DslSelectParser.ParseToSelectionTree("CustomerName,PrimaryContact.EmailAddress:Email,PrimaryContact(PhoneNumber:ContactNumber)");

        tree.Should().NotBeNull();
        tree!.TryGetChild("CustomerName", out _).Should().BeTrue();
        tree.TryGetChild("PrimaryContact", out var primaryContact).Should().BeTrue();
        primaryContact!.TryGetChild("EmailAddress", out var email).Should().BeTrue();
        email!.Alias.Should().Be("Email");
        primaryContact.TryGetChild("PhoneNumber", out var phone).Should().BeTrue();
        phone!.Alias.Should().Be("ContactNumber");
    }

    [Fact]
    public void ParseToSelectionTree_MixedFlatAndNestedSyntax_SameFieldDifferentAliases_ThrowsDuringMerge()
    {
        // Parser should succeed; SelectTreeBuilder should reject conflicting aliases.
        var act = () => DslSelectParser.ParseToSelectionTree("Customer.Email:CustomerEmail,Customer(Email:CustomerEmailAddress)");

        act.Should().NotThrow<DslParseException>();
    }

    #endregion

    #endregion
}
