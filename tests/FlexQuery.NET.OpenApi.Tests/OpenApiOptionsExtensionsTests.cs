using FlexQuery.NET.OpenApi;
using Microsoft.AspNetCore.OpenApi;
using Xunit;

namespace FlexQuery.NET.OpenApi.Tests;

public class OpenApiOptionsExtensionsTests
{
    [Fact]
    public void AddFlexQuery_RegistersTransformersWithoutThrowing()
    {
        // In Microsoft.AspNetCore.OpenApi 9.0.0 the transformer collections are not exposed
        // publicly, so we verify the fluent registration completes and returns the same options.
        // The actual transformer behavior is covered by FlexQuerySchemaTransformerTests and
        // FlexQueryOperationTransformerTests.
        var options = new OpenApiOptions();

        var result = options.AddFlexQuery();

        result.Should().BeSameAs(options);
    }
}

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddFlexQueryOpenApi_RegistersWithoutThrowing()
    {
        var services = new ServiceCollection();

        var result = services.AddFlexQueryOpenApi();

        result.Should().BeSameAs(services);

        var act = () => services.BuildServiceProvider();
        act.Should().NotThrow();
    }
}
