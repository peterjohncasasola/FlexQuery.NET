using FlexQuery.NET.Parsers.MiniOData;
using FlexQuery.NET.Parsers.MiniOData.Models;

namespace FlexQuery.NET.Tests.MiniOData;

public class MiniODataQueryParserTests
{
    [Fact]
    public void Parse_Filter_TranslatesFilterExpression()
    {
        var request = new MiniODataRequest
        {
            Filter = "name eq 'john'"
        };

        var result = ODataQueryParameterParser.Parse(request);

        result.Filter.Should().NotBeNull();
        result.Filter!.Filters.Should().HaveCount(1);
        result.Filter!.Filters[0].Field.Should().Be("name");
    }

    [Fact]
    public void Parse_OrderBy_SingleAsc_ProducesSortNode()
    {
        var request = new MiniODataRequest
        {
            OrderBy = "name"
        };

        var result = ODataQueryParameterParser.Parse(request);

        result.Sort.Should().HaveCount(1);
        result.Sort[0].Field.Should().Be("name");
        result.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Parse_OrderBy_SingleDesc_ProducesDescendingSort()
    {
        var request = new MiniODataRequest
        {
            OrderBy = "createdAt desc"
        };

        var result = ODataQueryParameterParser.Parse(request);

        result.Sort.Should().HaveCount(1);
        result.Sort[0].Field.Should().Be("createdAt");
        result.Sort[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void Parse_OrderBy_Multiple_ProducesMultipleSortNodes()
    {
        var request = new MiniODataRequest
        {
            OrderBy = "lastName asc, createdAt desc"
        };

        var result = ODataQueryParameterParser.Parse(request);

        result.Sort.Should().HaveCount(2);
        result.Sort[0].Field.Should().Be("lastName");
        result.Sort[0].Descending.Should().BeFalse();
        result.Sort[1].Field.Should().Be("createdAt");
        result.Sort[1].Descending.Should().BeTrue();
    }

    [Fact]
    public void Parse_OrderBy_SlashPath_ConvertsToDotNotation()
    {
        var request = new MiniODataRequest
        {
            OrderBy = "address/city desc"
        };

        var result = ODataQueryParameterParser.Parse(request);

        result.Sort[0].Field.Should().Be("address.city");
    }

    [Fact]
    public void Parse_Select_CommaSeparatedFields()
    {
        var request = new MiniODataRequest
        {
            Select = "id,name,email"
        };

        var result = ODataQueryParameterParser.Parse(request);

        result.Select.Should().BeEquivalentTo(new[] { "id", "name", "email" });
    }

    [Fact]
    public void Parse_Select_SlashPaths_ConvertToDotNotation()
    {
        var request = new MiniODataRequest
        {
            Select = "id,profile/name,address/city"
        };

        var result = ODataQueryParameterParser.Parse(request);

        result.Select.Should().Contain("profile.name");
        result.Select.Should().Contain("address.city");
    }

    [Fact]
    public void Parse_Top_SetsPageSize()
    {
        var request = new MiniODataRequest
        {
            Top = 10
        };

        var result = ODataQueryParameterParser.Parse(request);

        result.Paging.PageSize.Should().Be(10);
    }

    [Fact]
    public void Parse_Skip_SetsSkipCount()
    {
        var request = new MiniODataRequest
        {
            Top = 20,
            Skip = 20
        };

        var result = ODataQueryParameterParser.Parse(request);

        result.Paging.Page.Should().Be(2);
    }

    [Fact]
    public void Parse_SkipAndTop_CalculatesPage()
    {
        var request = new MiniODataRequest
        {
            Top = 10,
            Skip = 20
        };

        var result = ODataQueryParameterParser.Parse(request);

        result.Paging.Page.Should().Be(3);
        result.Paging.PageSize.Should().Be(10);
    }

    [Fact]
    public void Parse_Expand_SingleNavigation()
    {
        var request = new MiniODataRequest
        {
            Expand = "orders"
        };

        var result = ODataQueryParameterParser.Parse(request);

        result.Includes.Should().BeEquivalentTo(new[] { "orders" });
    }

    [Fact]
    public void Parse_Expand_MultipleNavigations()
    {
        var request = new MiniODataRequest
        {
            Expand = "orders,profile,addresses"
        };

        var result = ODataQueryParameterParser.Parse(request);

        result.Includes.Should().HaveCount(3);
        result.Includes.Should().Contain("orders");
        result.Includes.Should().Contain("profile");
        result.Includes.Should().Contain("addresses");
    }

    [Fact]
    public void Parse_Count_True()
    {
        var request = new MiniODataRequest
        {
            Count = true
        };

        var result = ODataQueryParameterParser.Parse(request);

        result.IncludeCount.Should().BeTrue();
    }

    [Fact]
    public void Parse_Count_False()
    {
        var request = new MiniODataRequest
        {
            Count = false
        };

        var result = ODataQueryParameterParser.Parse(request);

        result.IncludeCount.Should().BeFalse();
    }

    [Fact]
    public void Parse_AllParameters_ProducesCompleteQueryOptions()
    {
        var request = new MiniODataRequest
        {
            Filter = "age gt 18 and status eq 'active'",
            OrderBy = "name asc, createdAt desc",
            Select = "id,name,email",
            Top = 25,
            Skip = 50,
            Expand = "orders,profile",
            Count = true
        };

        var result = ODataQueryParameterParser.Parse(request);

        result.Filter.Should().NotBeNull();
        result.Filter!.Filters.Should().HaveCount(2);
        result.Sort.Should().HaveCount(2);
        result.Select.Should().HaveCount(3);
        result.Paging.PageSize.Should().Be(25);
        result.Paging.Page.Should().Be(3);
        result.Includes.Should().HaveCount(2);
        result.IncludeCount.Should().BeTrue();
    }

    [Fact]
    public void Parse_DefaultRequest_ReturnsDefaults()
    {
        var result = ODataQueryParameterParser.Parse(new MiniODataRequest());

        result.Filter.Should().BeNull();
        result.Sort.Should().BeEmpty();
        result.Select.Should().BeNull();
        result.Includes.Should().BeNull();
    }
}
