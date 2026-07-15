using System.Text.Json;
using FlexQuery.NET.Models;
using FlexQuery.NET.OpenApi.Transformers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;
using Xunit;

namespace FlexQuery.NET.OpenApi.Tests.Transformers;

public class FlexQuerySchemaTransformerTests
{
    private static OpenApiSchemaTransformerContext MakeContext(Type type)
    {
        return new OpenApiSchemaTransformerContext
        {
            DocumentName = "v1",
            JsonTypeInfo = JsonSerializerOptions.Default.GetTypeInfo(type),
            ParameterDescription = null,
            JsonPropertyInfo = null,
            ApplicationServices = null
        };
    }

    [Fact]
    public async Task TransformAsync_RegisteredType_SetsDescriptionAndExample()
    {
        var transformer = new FlexQuerySchemaTransformer();
        var schema = new OpenApiSchema();
        var context = MakeContext(typeof(FlexQueryRequest));

        await transformer.TransformAsync(schema, context, CancellationToken.None);

        schema.Description.Should().NotBeNullOrWhiteSpace();
        schema.Example.Should().NotBeNull();
    }

    [Fact]
    public async Task TransformAsync_UnregisteredType_IsNoOp()
    {
        var transformer = new FlexQuerySchemaTransformer();
        var schema = new OpenApiSchema { Description = "untouched" };
        var context = MakeContext(typeof(string));

        await transformer.TransformAsync(schema, context, CancellationToken.None);

        schema.Description.Should().Be("untouched");
        schema.Example.Should().BeNull();
    }
}

public class FlexQueryOperationTransformerTests
{
    [Fact]
    public async Task TransformAsync_SetsDescriptionForKnownParameters()
    {
        var transformer = new FlexQueryOperationTransformer();
        var operation = new OpenApiOperation
        {
            Parameters =
            {
                new OpenApiParameter { Name = "filter" },
                new OpenApiParameter { Name = "sort" },
                new OpenApiParameter { Name = "page" },
                new OpenApiParameter { Name = "pageSize" },
                new OpenApiParameter { Name = "select" },
                new OpenApiParameter { Name = "include" },
                new OpenApiParameter { Name = "other" }
            }
        };

        await transformer.TransformAsync(operation, default, CancellationToken.None);

        operation.Parameters.Should().Contain(p => p.Name == "filter" && !string.IsNullOrWhiteSpace(p.Description));
        operation.Parameters.Should().Contain(p => p.Name == "sort" && !string.IsNullOrWhiteSpace(p.Description));
        operation.Parameters.Should().Contain(p => p.Name == "page" && !string.IsNullOrWhiteSpace(p.Description));
        operation.Parameters.Should().Contain(p => p.Name == "pageSize" && !string.IsNullOrWhiteSpace(p.Description));
        operation.Parameters.Should().Contain(p => p.Name == "select" && !string.IsNullOrWhiteSpace(p.Description));
        operation.Parameters.Should().Contain(p => p.Name == "include" && !string.IsNullOrWhiteSpace(p.Description));
        operation.Parameters.Should().Contain(p => p.Name == "other" && p.Description == null);
    }

    [Fact]
    public async Task TransformAsync_NullParameters_IsNoOp()
    {
        var transformer = new FlexQueryOperationTransformer();
        var operation = new OpenApiOperation { Parameters = null };

        var act = async () => await transformer.TransformAsync(operation, default, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }
}
