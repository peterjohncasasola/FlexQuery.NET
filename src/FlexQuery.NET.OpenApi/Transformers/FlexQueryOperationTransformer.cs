using FlexQuery.NET.OpenApi.Documentation;
using Microsoft.AspNetCore.OpenApi;

#if NET9_0
using Microsoft.OpenApi.Models;
#else
using Microsoft.OpenApi;
#endif

namespace FlexQuery.NET.OpenApi.Transformers;

internal sealed class FlexQueryOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        foreach (var parameter in operation.Parameters)
        {
            var description = parameter.Name switch
            {
                "filter" => ParameterDocumentation.Filter,
                "sort" => ParameterDocumentation.Sort,
                "page" => ParameterDocumentation.Page,
                "pageSize" => ParameterDocumentation.PageSize,
                "select" => ParameterDocumentation.Select,
                "include" => ParameterDocumentation.Include,
                _ => null
            };

            if (description is not null)
                parameter.Description = description;
        }
        return Task.CompletedTask;
    }
}
