using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.MiniOData;

namespace FlexQuery.NET.Tests.MiniOData;

/// <summary>
/// Verifies that MiniOData parser failures are consistently wrapped in <see cref="QueryParseException"/>
/// (with an inner <see cref="MiniODataParseException"/>), matching the DSL and FQL parsers.
/// </summary>
public class MiniODataExceptionWrappingTests
{
    public MiniODataExceptionWrappingTests()
    {
        QueryParserRegistry.Register(QuerySyntax.MiniOData, new MiniODataQueryParser());
    }

    [Fact]
    public void Filter_Invalid_WrapsInQueryParseException()
    {
        var parameters = new FlexQueryParameters { Filter = "Price gt" };

        var act = () => QueryOptionsParser.Parse(parameters, QuerySyntax.MiniOData);

        var ex = act.Should().Throw<QueryParseException>().Which;
        ex.ParameterName.Should().Be("$filter");
        ex.InnerException.Should().BeOfType<MiniODataParseException>();
        ex.Message.Should().StartWith("Failed to parse '$filter' query parameter.");
    }

    [Fact]
    public void OrderBy_Invalid_WrapsInQueryParseException()
    {
        var parameters = new FlexQueryParameters { Sort = "Name sideways" };

        var act = () => QueryOptionsParser.Parse(parameters, QuerySyntax.MiniOData);

        var ex = act.Should().Throw<QueryParseException>().Which;
        ex.ParameterName.Should().Be("$orderby");
        ex.InnerException.Should().BeOfType<MiniODataParseException>();
        ex.Message.Should().StartWith("Failed to parse '$orderby' query parameter.");
    }

    [Fact]
    public void Select_Invalid_WrapsInQueryParseException()
    {
        var parameters = new FlexQueryParameters { Select = "Customer//Region" };

        var act = () => QueryOptionsParser.Parse(parameters, QuerySyntax.MiniOData);

        var ex = act.Should().Throw<QueryParseException>().Which;
        ex.ParameterName.Should().Be("$select");
        ex.InnerException.Should().BeOfType<MiniODataParseException>();
        ex.Message.Should().StartWith("Failed to parse '$select' query parameter.");
    }

    [Fact]
    public void Expand_Invalid_WrapsInQueryParseException()
    {
        var parameters = new FlexQueryParameters { Include = "Orders($filter=Status eq 'Pending')" };

        var act = () => QueryOptionsParser.Parse(parameters, QuerySyntax.MiniOData);

        var ex = act.Should().Throw<QueryParseException>().Which;
        ex.ParameterName.Should().Be("$expand");
        ex.InnerException.Should().BeOfType<MiniODataParseException>();
        ex.Message.Should().StartWith("Failed to parse '$expand' query parameter.");
    }

    [Fact]
    public void ValidParameters_DoNotThrow()
    {
        var parameters = new FlexQueryParameters
        {
            Filter = "name eq 'john'",
            Sort = "createdAt desc",
            Select = "Id,Name",
            Include = "Orders"
        };

        var act = () => QueryOptionsParser.Parse(parameters, QuerySyntax.MiniOData);

        act.Should().NotThrow();
    }
}
