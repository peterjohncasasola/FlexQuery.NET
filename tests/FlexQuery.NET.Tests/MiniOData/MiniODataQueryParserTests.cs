using FlexQuery.NET.Parsers.MiniOData;
using FlexQuery.NET.Models;
using FluentAssertions;

namespace FlexQuery.NET.Tests.MiniOData;

/// <summary>
/// Tests for the ODataQueryParameterParser — the main entry point that parses
/// all OData query parameters ($filter, $orderby, $select, $top, $skip, $expand, $count).
/// </summary>
public class MiniODataQueryParserTests
{
    // ========================
    // $filter
    // ========================

    [Fact]
    public void Parse_Filter_TranslatesFilterExpression()
    {
        var queryParams = new Dictionary<string, string>
        {
            ["$filter"] = "name eq 'john'"
        };

        var result = ODataQueryParameterParser.Parse(queryParams);

        result.Filter.Should().NotBeNull();
        result.Filter!.Filters.Should().HaveCount(1);
        result.Filter!.Filters[0].Field.Should().Be("name");
    }

    [Fact]
    public void Parse_Filter_WithoutDollarPrefix_StillWorks()
    {
        var queryParams = new Dictionary<string, string>
        {
            ["filter"] = "status eq 'active'"
        };

        var result = ODataQueryParameterParser.Parse(queryParams);

        result.Filter.Should().NotBeNull();
        result.Filter!.Filters[0].Field.Should().Be("status");
    }

    // ========================
    // $orderby
    // ========================

    [Fact]
    public void Parse_OrderBy_SingleAsc_ProducesSortNode()
    {
        var queryParams = new Dictionary<string, string>
        {
            ["$orderby"] = "name"
        };

        var result = ODataQueryParameterParser.Parse(queryParams);

        result.Sort.Should().HaveCount(1);
        result.Sort[0].Field.Should().Be("name");
        result.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_OrderBy_SingleDesc_ProducesDescendingSort()
    {
        var queryParams = new Dictionary<string, string>
        {
            ["$orderby"] = "createdAt desc"
        };

        var result = ODataQueryParameterParser.Parse(queryParams);

        result.Sort.Should().HaveCount(1);
        result.Sort[0].Field.Should().Be("createdAt");
        result.Sort[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void Parse_OrderBy_Multiple_ProducesMultipleSortNodes()
    {
        var queryParams = new Dictionary<string, string>
        {
            ["$orderby"] = "lastName asc, createdAt desc"
        };

        var result = ODataQueryParameterParser.Parse(queryParams);

        result.Sort.Should().HaveCount(2);
        result.Sort[0].Field.Should().Be("lastName");
        result.Sort[0].Descending.Should().BeFalse();
        result.Sort[1].Field.Should().Be("createdAt");
        result.Sort[1].Descending.Should().BeTrue();
    }

    [Fact]
    public void Parse_OrderBy_SlashPath_ConvertsToDotNotation()
    {
        var queryParams = new Dictionary<string, string>
        {
            ["$orderby"] = "address/city desc"
        };

        var result = ODataQueryParameterParser.Parse(queryParams);

        result.Sort[0].Field.Should().Be("address.city");
    }

    // ========================
    // $select
    // ========================

    [Fact]
    public void Parse_Select_CommaSeparatedFields()
    {
        var queryParams = new Dictionary<string, string>
        {
            ["$select"] = "id,name,email"
        };

        var result = ODataQueryParameterParser.Parse(queryParams);

        result.Select.Should().BeEquivalentTo(new[] { "id", "name", "email" });
    }

    [Fact]
    public void Parse_Select_SlashPaths_ConvertToDotNotation()
    {
        var queryParams = new Dictionary<string, string>
        {
            ["$select"] = "id,profile/name,address/city"
        };

        var result = ODataQueryParameterParser.Parse(queryParams);

        result.Select.Should().Contain("profile.name");
        result.Select.Should().Contain("address.city");
    }

    // ========================
    // $top
    // ========================

    [Fact]
    public void Parse_Top_SetsPageSize()
    {
        var queryParams = new Dictionary<string, string>
        {
            ["$top"] = "10"
        };

        var result = ODataQueryParameterParser.Parse(queryParams);

        result.Paging.PageSize.Should().Be(10);
        result.Top.Should().Be(10);
    }

    // ========================
    // $skip
    // ========================

    [Fact]
    public void Parse_Skip_SetsSkipCount()
    {
        var queryParams = new Dictionary<string, string>
        {
            ["$skip"] = "20"
        };

        var result = ODataQueryParameterParser.Parse(queryParams);

        result.Skip.Should().Be(20);
    }

    [Fact]
    public void Parse_SkipAndTop_CalculatesPage()
    {
        var queryParams = new Dictionary<string, string>
        {
            ["$top"] = "10",
            ["$skip"] = "20"
        };

        var result = ODataQueryParameterParser.Parse(queryParams);

        result.Paging.Page.Should().Be(3); // skip 20 / top 10 + 1 = page 3
        result.Paging.PageSize.Should().Be(10);
    }

    // ========================
    // $expand
    // ========================

    [Fact]
    public void Parse_Expand_SingleNavigation()
    {
        var queryParams = new Dictionary<string, string>
        {
            ["$expand"] = "orders"
        };

        var result = ODataQueryParameterParser.Parse(queryParams);

        result.Includes.Should().BeEquivalentTo(new[] { "orders" });
    }

    [Fact]
    public void Parse_Expand_MultipleNavigations()
    {
        var queryParams = new Dictionary<string, string>
        {
            ["$expand"] = "orders,profile,addresses"
        };

        var result = ODataQueryParameterParser.Parse(queryParams);

        result.Includes.Should().HaveCount(3);
        result.Includes.Should().Contain("orders");
        result.Includes.Should().Contain("profile");
        result.Includes.Should().Contain("addresses");
    }

    // ========================
    // $count
    // ========================

    [Fact]
    public void Parse_Count_True()
    {
        var queryParams = new Dictionary<string, string>
        {
            ["$count"] = "true"
        };

        var result = ODataQueryParameterParser.Parse(queryParams);

        result.IncludeCount.Should().BeTrue();
    }

    [Fact]
    public void Parse_Count_False()
    {
        var queryParams = new Dictionary<string, string>
        {
            ["$count"] = "false"
        };

        var result = ODataQueryParameterParser.Parse(queryParams);

        result.IncludeCount.Should().BeFalse();
    }

    // ========================
    // Combined Parameters
    // ========================

    [Fact]
    public void Parse_AllParameters_ProducesCompleteQueryOptions()
    {
        var queryParams = new Dictionary<string, string>
        {
            ["$filter"] = "age gt 18 and status eq 'active'",
            ["$orderby"] = "name asc, createdAt desc",
            ["$select"] = "id,name,email",
            ["$top"] = "25",
            ["$skip"] = "50",
            ["$expand"] = "orders,profile",
            ["$count"] = "true"
        };

        var result = ODataQueryParameterParser.Parse(queryParams);

        result.Filter.Should().NotBeNull();
        result.Filter!.Filters.Should().HaveCount(2);
        result.Sort.Should().HaveCount(2);
        result.Select.Should().HaveCount(3);
        result.Paging.PageSize.Should().Be(25);
        result.Skip.Should().Be(50);
        result.Includes.Should().HaveCount(2);
        result.IncludeCount.Should().BeTrue();
    }

    // ========================
    // Edge Cases
    // ========================

    [Fact]
    public void Parse_EmptyParams_ReturnsDefaultQueryOptions()
    {
        var queryParams = new Dictionary<string, string>();

        var result = ODataQueryParameterParser.Parse(queryParams);

        result.Filter.Should().BeNull();
        result.Sort.Should().BeEmpty();
        result.Select.Should().BeNull();
        result.Includes.Should().BeNull();
    }

    [Fact]
    public void Parse_CaseInsensitiveKeys()
    {
        var queryParams = new Dictionary<string, string>
        {
            ["$Filter"] = "name eq 'test'",
            ["$OrderBy"] = "id desc"
        };

        var result = ODataQueryParameterParser.Parse(queryParams);

        result.Filter.Should().NotBeNull();
        result.Sort.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_InvalidTop_IsIgnored()
    {
        var queryParams = new Dictionary<string, string>
        {
            ["$top"] = "not_a_number"
        };

        var result = ODataQueryParameterParser.Parse(queryParams);

        result.Top.Should().BeNull();
    }
}
