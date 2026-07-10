using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers.MiniOData;
using FlexQuery.NET.Parsers.MiniOData.Models;

namespace FlexQuery.NET.Tests.MiniOData;

/// <summary>
/// Strict grammar validation for MiniOData parsers, asserting the resulting <see cref="QueryOptions"/>.
/// Mirrors the strictness of the DSL and FQL parsers while staying faithful to the OData grammar.
/// </summary>
public class MiniODataStrictValidationTests
{
    // ======================== Filter ========================

    [Fact]
    public void Filter_Valid_SimpleComparison_Parsed()
    {
        var result = ODataQueryParameterParser.Parse(new MiniODataRequest { Filter = "name eq 'john'" });

        result.Filter.Should().NotBeNull();
        result.Filter!.Filters.Should().ContainSingle();
        result.Filter.Filters[0].Field.Should().Be("name");
        result.Filter.Filters[0].Value.Should().Be("john");
    }

    [Fact]
    public void Filter_Invalid_MissingValue_Throws()
    {
        Action act = () => ODataFilterParser.Parse("Price gt");
        act.Should().Throw<MiniODataParseException>();
    }

    [Fact]
    public void Filter_Invalid_UnexpectedCharacter_Throws()
    {
        Action act = () => ODataFilterParser.Parse("Price >");
        act.Should().Throw<MiniODataParseException>();
    }

    [Fact]
    public void Filter_Invalid_LeadingLogical_Throws()
    {
        Action act = () => ODataFilterParser.Parse("and Price gt 100");
        act.Should().Throw<MiniODataParseException>();
    }

    [Fact]
    public void Filter_Invalid_UnterminatedString_Throws()
    {
        Action act = () => ODataFilterParser.Parse("name eq 'unterminated");
        act.Should().Throw<MiniODataParseException>();
    }

    // ======================== OrderBy ========================

    [Fact]
    public void OrderBy_Valid_SingleAsc_Parsed()
    {
        var result = ODataQueryParameterParser.Parse(new MiniODataRequest { OrderBy = "Name" });

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("Name");
        result.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void OrderBy_Valid_SingleDesc_Parsed()
    {
        var result = ODataQueryParameterParser.Parse(new MiniODataRequest { OrderBy = "createdAt desc" });

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("createdAt");
        result.Sort[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void OrderBy_Valid_Multiple_Parsed()
    {
        var result = ODataQueryParameterParser.Parse(new MiniODataRequest { OrderBy = "lastName asc, createdAt desc" });

        result.Sort.Should().HaveCount(2);
        result.Sort[0].Field.Should().Be("lastName");
        result.Sort[0].Descending.Should().BeFalse();
        result.Sort[1].Field.Should().Be("createdAt");
        result.Sort[1].Descending.Should().BeTrue();
    }

    [Fact]
    public void OrderBy_Valid_NestedPath_ConvertsToDotNotation()
    {
        var result = ODataQueryParameterParser.Parse(new MiniODataRequest { OrderBy = "address/city desc" });

        result.Sort[0].Field.Should().Be("address.city");
    }

    [Fact]
    public void OrderBy_Invalid_UnknownDirection_Throws()
    {
        Action act = () => ODataOrderByParser.Parse("Name sideways");
        act.Should().Throw<MiniODataParseException>();
    }

    [Fact]
    public void OrderBy_Invalid_DuplicateDirection_Throws()
    {
        Action act = () => ODataOrderByParser.Parse("Name desc desc");
        act.Should().Throw<MiniODataParseException>();
    }

    [Fact]
    public void OrderBy_Invalid_Empty_Throws()
    {
        Action act = () => ODataOrderByParser.Parse("");
        act.Should().Throw<MiniODataParseException>();
    }

    [Fact]
    public void OrderBy_Invalid_EmptyItem_Throws()
    {
        Action act = () => ODataOrderByParser.Parse("Name,");
        act.Should().Throw<MiniODataParseException>();
    }

    [Fact]
    public void OrderBy_Invalid_InvalidPath_Throws()
    {
        Action act = () => ODataOrderByParser.Parse("Customer//Region desc");
        act.Should().Throw<MiniODataParseException>();
    }

    // ======================== Select ========================

    [Fact]
    public void Select_Valid_CommaSeparated_Parsed()
    {
        var result = ODataQueryParameterParser.Parse(new MiniODataRequest { Select = "Id,Name,Email" });

        result.Select.Should().BeEquivalentTo(new[] { "Id", "Name", "Email" });
    }

    [Fact]
    public void Select_Valid_NestedPath_ConvertsToDotNotation()
    {
        var result = ODataQueryParameterParser.Parse(new MiniODataRequest { Select = "Customer/Name" });

        result.Select.Should().Contain("Customer.Name");
    }

    [Fact]
    public void Select_Invalid_Empty_Throws()
    {
        Action act = () => ODataSelectParser.Parse("");
        act.Should().Throw<MiniODataParseException>();
    }

    [Fact]
    public void Select_Invalid_TrailingComma_Throws()
    {
        Action act = () => ODataSelectParser.Parse(",");
        act.Should().Throw<MiniODataParseException>();
    }

    [Fact]
    public void Select_Invalid_DoubleSlash_Throws()
    {
        Action act = () => ODataSelectParser.Parse("Customer//Region");
        act.Should().Throw<MiniODataParseException>();
    }

    // ======================== Expand ========================

    [Fact]
    public void Expand_Valid_Single_Parsed()
    {
        var result = ODataQueryParameterParser.Parse(new MiniODataRequest { Expand = "Orders" });

        result.Includes.Should().BeEquivalentTo(new[] { "Orders" });
    }

    [Fact]
    public void Expand_Valid_Multiple_Parsed()
    {
        var result = ODataQueryParameterParser.Parse(new MiniODataRequest { Expand = "Orders,Profile,Addresses" });

        result.Includes.Should().HaveCount(3);
        result.Includes.Should().Contain("Orders");
        result.Includes.Should().Contain("Profile");
        result.Includes.Should().Contain("Addresses");
    }

    [Fact]
    public void Expand_Invalid_Empty_Throws()
    {
        Action act = () => MiniODataExpandParser.Parse("");
        act.Should().Throw<MiniODataParseException>();
    }

    [Fact]
    public void Expand_Invalid_OpenParen_Throws()
    {
        Action act = () => MiniODataExpandParser.Parse("Orders(");
        act.Should().Throw<MiniODataParseException>();
    }

    [Fact]
    public void Expand_Invalid_NestedFilterOption_Throws()
    {
        Action act = () => MiniODataExpandParser.Parse("Orders($filter=Status eq 'Pending')");
        act.Should().Throw<MiniODataParseException>();
    }

    [Fact]
    public void Expand_Invalid_CloseParen_Throws()
    {
        Action act = () => MiniODataExpandParser.Parse("Orders())");
        act.Should().Throw<MiniODataParseException>();
    }

    [Fact]
    public void Expand_Invalid_TrailingComma_Throws()
    {
        Action act = () => MiniODataExpandParser.Parse(",");
        act.Should().Throw<MiniODataParseException>();
    }

    [Fact]
    public void Expand_Invalid_DoubleSlash_Throws()
    {
        Action act = () => MiniODataExpandParser.Parse("Customer//Orders");
        act.Should().Throw<MiniODataParseException>();
    }

    // ======================== Empty parameter values ========================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void OrderBy_EmptyValue_Throws(string value)
    {
        Action act = () => ODataOrderByParser.Parse(value);
        act.Should().Throw<MiniODataParseException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Select_EmptyValue_Throws(string value)
    {
        Action act = () => ODataSelectParser.Parse(value);
        act.Should().Throw<MiniODataParseException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Expand_EmptyValue_Throws(string value)
    {
        Action act = () => MiniODataExpandParser.Parse(value);
        act.Should().Throw<MiniODataParseException>();
    }
}
