using FlexQuery.NET.OpenApi.Documentation;
using Xunit;

namespace FlexQuery.NET.OpenApi.Tests.Documentation;

public class DocumentationContentTests
{
    [Fact]
    public void ParameterDocumentation_ValuesAreNonEmpty()
    {
        ParameterDocumentation.Filter.Should().NotBeNullOrWhiteSpace();
        ParameterDocumentation.Sort.Should().NotBeNullOrWhiteSpace();
        ParameterDocumentation.Page.Should().NotBeNullOrWhiteSpace();
        ParameterDocumentation.PageSize.Should().NotBeNullOrWhiteSpace();
        ParameterDocumentation.Select.Should().NotBeNullOrWhiteSpace();
        ParameterDocumentation.Include.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void SchemaDocumentation_ValuesAreNonEmpty()
    {
        SchemaDocumentation.FlexQueryRequest.Should().NotBeNullOrWhiteSpace();
        SchemaDocumentation.FlexQueryParameters.Should().NotBeNullOrWhiteSpace();
        SchemaDocumentation.QueryResult.Should().NotBeNullOrWhiteSpace();
        SchemaDocumentation.FilterGroup.Should().NotBeNullOrWhiteSpace();
        SchemaDocumentation.FilterCondition.Should().NotBeNullOrWhiteSpace();
        SchemaDocumentation.SortNode.Should().NotBeNullOrWhiteSpace();
        SchemaDocumentation.PagingOptions.Should().NotBeNullOrWhiteSpace();
        SchemaDocumentation.AggregateModel.Should().NotBeNullOrWhiteSpace();
        SchemaDocumentation.HavingConditionExpression.Should().NotBeNullOrWhiteSpace();
        SchemaDocumentation.IncludeNode.Should().NotBeNullOrWhiteSpace();
        SchemaDocumentation.ProjectionMode.Should().NotBeNullOrWhiteSpace();
        SchemaDocumentation.LogicOperator.Should().NotBeNullOrWhiteSpace();
        SchemaDocumentation.AggregateFunction.Should().NotBeNullOrWhiteSpace();
    }
}
