using FlexQuery.NET.OpenApi.Documentation;
using FlexQuery.NET.Models;
using Xunit;

namespace FlexQuery.NET.OpenApi.Tests.Documentation;

public class FlexQueryDocumentationRegistryTests
{
    [Theory]
    [InlineData(typeof(FlexQueryRequest))]
    [InlineData(typeof(FlexQueryParameters))]
    [InlineData(typeof(FilterGroup))]
    [InlineData(typeof(FilterCondition))]
    [InlineData(typeof(SortNode))]
    [InlineData(typeof(PagingOptions))]
    [InlineData(typeof(AggregateModel))]
    [InlineData(typeof(HavingConditionNode))]
    [InlineData(typeof(IncludeNode))]
    [InlineData(typeof(ProjectionMode))]
    [InlineData(typeof(LogicOperator))]
    [InlineData(typeof(AggregateFunction))]
    public void TryGet_RegisteredModelType_ReturnsTrue(Type type)
    {
        var found = FlexQueryDocumentationRegistry.TryGet(type, out var doc);

        found.Should().BeTrue();
        doc.Description.Should().NotBeNull();
    }

    [Fact]
    public void TryGet_QueryResultGeneric_ReturnsTrue()
    {
        var found = FlexQueryDocumentationRegistry.TryGet(typeof(QueryResult<Dictionary<string, object>>), out var doc);

        found.Should().BeTrue();
        doc.Description.Should().NotBeNull();
        doc.Example.Should().NotBeNull();
    }

    [Fact]
    public void TryGet_UnknownType_ReturnsFalse()
    {
        var found = FlexQueryDocumentationRegistry.TryGet(typeof(string), out var doc);

        found.Should().BeFalse();
        doc.Should().Be(default(FlexQueryDocumentation));
    }
}
