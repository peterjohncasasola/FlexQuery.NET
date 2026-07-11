using FlexQuery.NET.OpenApi.Transformers;
using Microsoft.AspNetCore.OpenApi;

namespace FlexQuery.NET.OpenApi;

/// <summary>
/// Provides extension methods for <see cref="OpenApiOptions"/> to integrate FlexQuery.NET.
/// </summary>
public static class OpenApiOptionsExtensions
{
    /// <summary>
    /// Configures the OpenAPI generation to include FlexQuery-specific schema and operation transformations.
    /// </summary>
    /// <param name="options">The <see cref="OpenApiOptions"/> instance to configure.</param>
    /// <returns>The configured <see cref="OpenApiOptions"/> instance for method chaining.</returns>
    public static OpenApiOptions AddFlexQuery(this OpenApiOptions options)
    {
        options.AddSchemaTransformer<FlexQuerySchemaTransformer>();
        options.AddOperationTransformer<FlexQueryOperationTransformer>();
        return options;
    }
}