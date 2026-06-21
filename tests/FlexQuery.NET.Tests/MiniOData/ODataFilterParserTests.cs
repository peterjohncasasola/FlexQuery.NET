using FlexQuery.NET.Constants;
using FlexQuery.NET.Parsers.MiniOData;
using FluentAssertions;

namespace FlexQuery.NET.Tests.MiniOData;

/// <summary>
/// Tests for the OData filter expression parser.
/// Validates that all supported OData $filter syntax correctly produces
/// the unified FlexQuery FilterGroup AST.
/// </summary>
public class ODataFilterParserTests
{
    // ========================
    // Simple Comparisons
    // ========================

    [Fact]
    public void Parse_EqualString_ProducesCorrectFilterGroup()
    {
        var result = ODataFilterParser.Parse("name eq 'john'");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Field.Should().Be("name");
        result.Filters[0].Operator.Should().Be(FilterOperators.Equal);
        result.Filters[0].Value.Should().Be("john");
    }

    [Fact]
    public void Parse_NotEqual_ProducesNeqOperator()
    {
        var result = ODataFilterParser.Parse("status ne 'inactive'");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Field.Should().Be("status");
        result.Filters[0].Operator.Should().Be(FilterOperators.NotEqual);
        result.Filters[0].Value.Should().Be("inactive");
    }

    [Fact]
    public void Parse_GreaterThan_ProducesGtOperator()
    {
        var result = ODataFilterParser.Parse("age gt 18");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Field.Should().Be("age");
        result.Filters[0].Operator.Should().Be(FilterOperators.GreaterThan);
        result.Filters[0].Value.Should().Be("18");
    }

    [Fact]
    public void Parse_GreaterThanOrEqual_ProducesGteOperator()
    {
        var result = ODataFilterParser.Parse("price ge 9.99");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Operator.Should().Be(FilterOperators.GreaterThanOrEq);
        result.Filters[0].Value.Should().Be("9.99");
    }

    [Fact]
    public void Parse_LessThan_ProducesLtOperator()
    {
        var result = ODataFilterParser.Parse("quantity lt 100");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Operator.Should().Be(FilterOperators.LessThan);
        result.Filters[0].Value.Should().Be("100");
    }

    [Fact]
    public void Parse_LessThanOrEqual_ProducesLteOperator()
    {
        var result = ODataFilterParser.Parse("score le 50");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Operator.Should().Be(FilterOperators.LessThanOrEq);
    }

    // ========================
    // Boolean & Null Values
    // ========================

    [Fact]
    public void Parse_BooleanTrue_ProducesTrueValue()
    {
        var result = ODataFilterParser.Parse("isActive eq true");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Value.Should().Be("true");
    }

    [Fact]
    public void Parse_BooleanFalse_ProducesFalseValue()
    {
        var result = ODataFilterParser.Parse("isDeleted eq false");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Value.Should().Be("false");
    }

    [Fact]
    public void Parse_NullCheck_ProducesIsNullOperator()
    {
        var result = ODataFilterParser.Parse("deletedAt eq null");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Operator.Should().Be(FilterOperators.IsNull);
    }

    [Fact]
    public void Parse_NotNullCheck_ProducesIsNotNullOperator()
    {
        var result = ODataFilterParser.Parse("email ne null");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Operator.Should().Be(FilterOperators.IsNotNull);
    }

    // ========================
    // Function Calls
    // ========================

    [Fact]
    public void Parse_Contains_ProducesContainsOperator()
    {
        var result = ODataFilterParser.Parse("contains(name,'john')");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Field.Should().Be("name");
        result.Filters[0].Operator.Should().Be(FilterOperators.Contains);
        result.Filters[0].Value.Should().Be("john");
    }

    [Fact]
    public void Parse_StartsWith_ProducesStartsWithOperator()
    {
        var result = ODataFilterParser.Parse("startswith(name,'jo')");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Operator.Should().Be(FilterOperators.StartsWith);
        result.Filters[0].Value.Should().Be("jo");
    }

    [Fact]
    public void Parse_EndsWith_ProducesEndsWithOperator()
    {
        var result = ODataFilterParser.Parse("endswith(email,'.com')");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Operator.Should().Be(FilterOperators.EndsWith);
        result.Filters[0].Value.Should().Be(".com");
    }

    // ========================
    // Logical Operators
    // ========================

    [Fact]
    public void Parse_And_CombinesFiltersWithAndLogic()
    {
        var result = ODataFilterParser.Parse("age gt 18 and status eq 'active'");

        result.Logic.Should().Be(FlexQuery.NET.Models.LogicOperator.And);
        result.Filters.Should().HaveCount(2);
        result.Filters[0].Field.Should().Be("age");
        result.Filters[1].Field.Should().Be("status");
    }

    [Fact]
    public void Parse_Or_CombinesFiltersWithOrLogic()
    {
        var result = ODataFilterParser.Parse("status eq 'active' or status eq 'pending'");

        result.Logic.Should().Be(FlexQuery.NET.Models.LogicOperator.Or);
        result.Filters.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_Not_NegatesInnerGroup()
    {
        var result = ODataFilterParser.Parse("not (status eq 'deleted')");

        result.IsNegated.Should().BeTrue();
        result.Filters.Should().HaveCount(1);
        result.Filters[0].Field.Should().Be("status");
    }

    [Fact]
    public void Parse_ComplexLogic_MixedAndOr()
    {
        var result = ODataFilterParser.Parse("name eq 'john' and (age gt 18 or status eq 'vip')");

        // Should have AND at top level
        result.Logic.Should().Be(FlexQuery.NET.Models.LogicOperator.And);
    }

    // ========================
    // Grouping (Parentheses)
    // ========================

    [Fact]
    public void Parse_Parentheses_RespectsGrouping()
    {
        var result = ODataFilterParser.Parse("(status eq 'active' or status eq 'pending') and age gt 18");

        result.Logic.Should().Be(FlexQuery.NET.Models.LogicOperator.And);
    }

    // ========================
    // Nested Property Paths
    // ========================

    [Fact]
    public void Parse_SlashPath_ConvertsToDotNotation()
    {
        var result = ODataFilterParser.Parse("address/city eq 'NYC'");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Field.Should().Be("address.city");
    }

    [Fact]
    public void Parse_DeepPath_ConvertsMultiLevelPath()
    {
        var result = ODataFilterParser.Parse("contains(profile/address/city,'York')");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Field.Should().Be("profile.address.city");
    }

    // ========================
    // Lambda Navigation (any/all)
    // ========================

    [Fact]
    public void Parse_AnyLambda_ProducesAnyOperator()
    {
        var result = ODataFilterParser.Parse("orders/any(o: o/status eq 'Cancelled')");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Field.Should().Be("orders");
        result.Filters[0].Operator.Should().Be("any");
        result.Filters[0].ScopedFilter.Should().NotBeNull();
        result.Filters[0].ScopedFilter!.Filters.Should().HaveCount(1);
        result.Filters[0].ScopedFilter!.Filters[0].Field.Should().Be("status");
        result.Filters[0].ScopedFilter!.Filters[0].Value.Should().Be("Cancelled");
    }

    [Fact]
    public void Parse_AllLambda_ProducesAllOperator()
    {
        var result = ODataFilterParser.Parse("items/all(i: i/price gt 10)");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Field.Should().Be("items");
        result.Filters[0].Operator.Should().Be("all");
        result.Filters[0].ScopedFilter.Should().NotBeNull();
    }

    [Fact]
    public void Parse_EmptyAny_ProducesAnyWithNoFilter()
    {
        var result = ODataFilterParser.Parse("orders/any()");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Operator.Should().Be("any");
        result.Filters[0].ScopedFilter.Should().BeNull();
    }

    // ========================
    // IN operator
    // ========================

    [Fact]
    public void Parse_InOperator_ProducesInWithCommaSeparatedValues()
    {
        var result = ODataFilterParser.Parse("status in ('active','pending','review')");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Operator.Should().Be(FilterOperators.In);
        result.Filters[0].Value.Should().Be("active,pending,review");
    }

    // ========================
    // Edge Cases
    // ========================

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyFilterGroup()
    {
        var result = ODataFilterParser.Parse("");
        result.Filters.Should().BeEmpty();
        result.Groups.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsEmptyFilterGroup()
    {
        var result = ODataFilterParser.Parse("   ");
        result.Filters.Should().BeEmpty();
    }

    [Fact]
    public void Parse_EscapedQuote_PreservesQuoteInValue()
    {
        var result = ODataFilterParser.Parse("name eq 'O''Brien'");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Value.Should().Be("O'Brien");
    }

    [Fact]
    public void Parse_NegativeNumber_ParsesCorrectly()
    {
        var result = ODataFilterParser.Parse("balance lt -100");

        result.Filters.Should().HaveCount(1);
        result.Filters[0].Value.Should().Be("-100");
    }

    [Fact]
    public void Parse_InvalidExpression_ThrowsParseException()
    {
        Action act = () => ODataFilterParser.Parse("name INVALID 'test'");
        act.Should().Throw<MiniODataParseException>();
    }

    [Fact]
    public void Parse_MultipleAndsFlattened_ProducesAllConditions()
    {
        var result = ODataFilterParser.Parse("a eq '1' and b eq '2' and c eq '3'");

        result.Logic.Should().Be(FlexQuery.NET.Models.LogicOperator.And);
        result.Filters.Should().HaveCount(3);
    }
}
