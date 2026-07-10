using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.MiniOData;

namespace FlexQuery.NET.Tests.MiniOData;

public class MiniODataIntegrationTests
{
    public MiniODataIntegrationTests()
    {
        // Ensure MiniOData parser is registered for integration tests.
        // This is normally handled by services.AddMiniOData() in a real app.
        QueryParserRegistry.Register(QuerySyntax.MiniOData, new MiniODataQueryParser());
    }

    [Fact]
    public void ExplicitSyntax_ODataParameters_UsesMiniODataParser()
    {
        // Arrange
        var parameters = new FlexQueryParameters
        {
            Filter = "name eq 'john'",
            Sort = "age desc"
        };

        // Act
        var options = QueryOptionsParser.Parse(parameters, QuerySyntax.MiniOData);

        // Assert
        options.Filter.Should().NotBeNull();
        options.Filter!.Filters.Should().HaveCount(1);
        options.Filter!.Filters[0].Field.Should().Be("name");
        options.Filter!.Filters[0].Value.Should().Be("john");

        options.Sort.Should().HaveCount(1);
        options.Sort[0].Field.Should().Be("age");
        options.Sort[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void DefaultSyntax_WithNativeParameters_UsesDslParser()
    {
        // Arrange
        var parameters = new FlexQueryParameters
        {
            Filter = "name:eq:john",
            Sort = "age:desc"
        };

        // Act
        var options = QueryOptionsParser.Parse(parameters);

        // Assert
        options.Filter.Should().NotBeNull();
        options.Filter!.Filters.Should().HaveCount(1);
        options.Filter!.Filters[0].Field.Should().Be("name");
        options.Filter!.Filters[0].Value.Should().Be("john");

        options.Sort.Should().HaveCount(1);
        options.Sort[0].Field.Should().Be("age");
        options.Sort[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void ExplicitSyntax_OverridesDefault()
    {
        // Arrange
        var parameters = new FlexQueryParameters
        {
            Filter = "name eq 'john'" // OData syntax in Native DSL property
        };

        // Act & Assert
        // DSL should reject OData syntax
        var dslAct = () => QueryOptionsParser.Parse(parameters, QuerySyntax.NativeDsl);
        dslAct.Should().Throw<FlexQueryException>();

        // MiniOData should parse OData syntax
        var optionsOData = QueryOptionsParser.Parse(parameters, QuerySyntax.MiniOData);
        optionsOData.Filter.Should().NotBeNull();
        optionsOData.Filter!.Filters[0].Value.Should().Be("john");
    }
}
