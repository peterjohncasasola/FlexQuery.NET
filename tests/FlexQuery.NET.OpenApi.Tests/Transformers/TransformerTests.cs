using System.Reflection;
using System.Text.Json;
using FlexQuery.NET.AspNetCore.Attributes;
using FlexQuery.NET.Models;
using FlexQuery.NET.OpenApi.Documentation;
using FlexQuery.NET.OpenApi.Transformers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
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

    [Fact]
    public async Task TransformAsync_ActionAttribute_EnrichesSelectDescription()
    {
        var transformer = new FlexQueryOperationTransformer();
        var operation = new OpenApiOperation
        {
            Parameters =
            {
                new OpenApiParameter { Name = "select", Description = "Base description" }
            }
        };
        var context = MakeContextWithAttribute(typeof(AttributedController).GetMethod(nameof(AttributedController.ActionWithSelectable))!);

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        var selectParam = operation.Parameters.Single(p => p.Name == "select");
        selectParam.Description.Should().Contain("Allowed fields");
        selectParam.Description.Should().Contain("Id");
        selectParam.Description.Should().Contain("Name");
    }

    [Fact]
    public async Task TransformAsync_ActionAttribute_EnrichesFilterSortIncludeDescriptions()
    {
        var transformer = new FlexQueryOperationTransformer();
        var operation = new OpenApiOperation
        {
            Parameters =
            {
                new OpenApiParameter { Name = "filter", Description = "Base filter" },
                new OpenApiParameter { Name = "sort", Description = "Base sort" },
                new OpenApiParameter { Name = "include", Description = "Base include" }
            }
        };
        var context = MakeContextWithAttribute(typeof(AttributedController).GetMethod(nameof(AttributedController.ActionWithFullGovernance))!);

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        operation.Parameters.Single(p => p.Name == "filter").Description.Should().Contain("Filterable fields").And.Contain("Status");
        operation.Parameters.Single(p => p.Name == "sort").Description.Should().Contain("Sortable fields").And.Contain("CreatedAt");
        operation.Parameters.Single(p => p.Name == "include").Description.Should().Contain("Expandable properties").And.Contain("Orders");
    }

    [Fact]
    public async Task TransformAsync_NoAttribute_NoChangeAndNoExtension()
    {
        var transformer = new FlexQueryOperationTransformer();
        var operation = new OpenApiOperation
        {
            Parameters =
            {
                new OpenApiParameter { Name = "select", Description = "Base description" }
            }
        };
        var context = MakeContextWithAttribute(typeof(PlainController).GetMethod(nameof(PlainController.Action))!, typeof(PlainController));

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        operation.Parameters.Single(p => p.Name == "select").Description.Should().Be(ParameterDocumentation.Select);
        operation.Extensions.Should().NotContainKey("x-flexquery");
    }

    [Fact]
    public async Task TransformAsync_DefaultContext_GracefulFallback()
    {
        var transformer = new FlexQueryOperationTransformer();
        var operation = new OpenApiOperation
        {
            Parameters =
            {
                new OpenApiParameter { Name = "select", Description = "Base description" }
            }
        };

        var act = async () => await transformer.TransformAsync(operation, default, CancellationToken.None);
        await act.Should().NotThrowAsync();

        operation.Parameters.Single(p => p.Name == "select").Description.Should().Be(ParameterDocumentation.Select);
        operation.Extensions.Should().NotContainKey("x-flexquery");
    }

    [Fact]
    public async Task TransformAsync_Attribute_EmitsVendorExtension()
    {
        var transformer = new FlexQueryOperationTransformer();
        var operation = new OpenApiOperation();
        var context = MakeContextWithAttribute(typeof(AttributedController).GetMethod(nameof(AttributedController.ActionWithFullGovernance))!);

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        operation.Extensions.Should().ContainKey("x-flexquery");
    }

    [Fact]
    public async Task TransformAsync_Attribute_VendorExtensionOmitsUnconfiguredProperties()
    {
        var transformer = new FlexQueryOperationTransformer();
        var operation = new OpenApiOperation();
        var context = MakeContextWithAttribute(typeof(AttributedController).GetMethod(nameof(AttributedController.ActionWithSelectable))!);

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        var extension = operation.Extensions["x-flexquery"];
        extension.Should().NotBeNull();
    }

    private static OpenApiOperationTransformerContext MakeContextWithAttribute(MethodInfo? methodInfo, Type? controllerType = null)
    {
        var actualControllerType = controllerType ?? typeof(AttributedController);
        var descriptor = new ControllerActionDescriptor
        {
            ControllerTypeInfo = actualControllerType.GetTypeInfo(),
            MethodInfo = methodInfo ?? actualControllerType.GetMethod("Action")!,
            ControllerName = actualControllerType.Name,
            ActionName = "Action"
        };

        var apiDescription = new ApiDescription
        {
            ActionDescriptor = descriptor
        };

        return new OpenApiOperationTransformerContext
        {
            DocumentName = "v1",
            Description = apiDescription,
            ApplicationServices = null
        };
    }

    [FieldAccess(Selectable = new[] { "Id", "Name" })]
    private class AttributedController
    {
        public IActionResult ActionWithSelectable() => null!;

        [FieldAccess(
            Selectable = new[] { "Id", "OrderNo", "Total" },
            Filterable = new[] { "Status", "Total" },
            Sortable = new[] { "CreatedAt" },
            AllowedIncludes = new[] { "Customer", "Items" })]
        public IActionResult ActionWithFullGovernance() => null!;

        public IActionResult ActionWithoutAttribute() => null!;
    }

    private class PlainController
    {
        public IActionResult Action() => null!;
    }
}
