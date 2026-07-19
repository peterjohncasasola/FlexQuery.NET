using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FlexQuery.NET.AspNetCore.Attributes;
using FlexQuery.NET.OpenApi.Documentation;
using FlexQuery.NET.OpenApi.Transformers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;

#if NET9_0
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
#else
using Microsoft.OpenApi;
#endif

namespace FlexQuery.NET.OpenApi.Transformers;

internal sealed class FlexQueryOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (operation.Parameters == null) return Task.CompletedTask;
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

        var attribute = ResolveFieldAccessAttribute(context);
        if (attribute == null)
            return Task.CompletedTask;

        foreach (var parameter in operation.Parameters)
        {
            var enriched = parameter.Name switch
            {
                "select" => AppendDescription(parameter.Description, "Allowed fields", attribute) ??
                            AppendDescription(parameter.Description, title: "", attribute, isFieldSection: false),
                "filter" => AppendDescription(parameter.Description, "Filterable fields",attribute),
                "sort" => AppendDescription(parameter.Description, "Sortable fields", attribute),
                "include" => AppendDescription(parameter.Description, "Expandable properties", attribute),
                _ => null
            };

            if (enriched is not null)
                parameter.Description = enriched;
        }

#if NET9_0
        var governance = new OpenApiObject();

        if (attribute.Allowed?.Length > 0)
            governance["allowedFields"] = ToOpenApiArray(attribute.Allowed);

        if (attribute.Blocked?.Length > 0)
            governance["blockedFields"] = ToOpenApiArray(attribute.Blocked);

        if (attribute.Filterable?.Length > 0)
            governance["filterableFields"] = ToOpenApiArray(attribute.Filterable);

        if (attribute.Sortable?.Length > 0)
            governance["sortableFields"] = ToOpenApiArray(attribute.Sortable);

        if (attribute.Selectable?.Length > 0)
            governance["selectableFields"] = ToOpenApiArray(attribute.Selectable);

        if (attribute.Groupable?.Length > 0)
            governance["groupableFields"] = ToOpenApiArray(attribute.Groupable);

        if (attribute.Aggregatable?.Length > 0)
            governance["aggregatableFields"] = ToOpenApiArray(attribute.Aggregatable);

        if (attribute.AllowedIncludes?.Length > 0)
            governance["allowedIncludes"] = ToOpenApiArray(attribute.AllowedIncludes);

        if (!string.IsNullOrEmpty(attribute.DefaultSortField))
            governance["defaultSortField"] = new OpenApiString(attribute.DefaultSortField);

        if (!string.IsNullOrEmpty(attribute.DefaultSortDirection))
            governance["defaultSortDirection"] = new OpenApiString(attribute.DefaultSortDirection);

        if (attribute.MaxDepth > 0)
            governance["maxDepth"] = new OpenApiInteger(attribute.MaxDepth);

        operation.Extensions["x-flexquery"] = new OpenApiObject
        {
            ["governance"] = governance
        };
#else
        var governanceNode = JsonSerializer.SerializeToNode(new
        {
            allowedFields = attribute.Allowed,
            blockedFields = attribute.Blocked,
            filterableFields = attribute.Filterable,
            sortableFields = attribute.Sortable,
            selectableFields = attribute.Selectable,
            groupableFields = attribute.Groupable,
            aggregatableFields = attribute.Aggregatable,
            allowedIncludes = attribute.AllowedIncludes,
            defaultSortField = attribute.DefaultSortField,
            defaultSortDirection = attribute.DefaultSortDirection,
            maxDepth = attribute.MaxDepth > 0 ? (int?)attribute.MaxDepth : null
        });

        var trimmed = TrimNulls(governanceNode);
        if (trimmed is { Count: > 0 })
        {
            operation.Extensions!["x-flexquery"] = new JsonNodeExtension(trimmed);
        }
#endif

        return Task.CompletedTask;
    }

    private static FieldAccessAttribute? ResolveFieldAccessAttribute(OpenApiOperationTransformerContext context)
    {
        if ((context?.Description?.ActionDescriptor) is not ControllerActionDescriptor controller)
            return null;

        return controller.MethodInfo?.GetCustomAttribute<FieldAccessAttribute>()
            ?? controller.ControllerTypeInfo?.GetCustomAttribute<FieldAccessAttribute>();
    }

    private static string? AppendDescription(string? description, string title,  FieldAccessAttribute attribute, bool isFieldSection = true)
    {
        if (isFieldSection)
            return ParameterDescriptionBuilder.AppendFieldSection(description, title, attribute.Selectable);

        return ParameterDescriptionBuilder.AppendExampleSection(description, attribute.Selectable);
    }

#if NET9_0
    private static OpenApiArray ToOpenApiArray(string[] values)
    {
        var array = new OpenApiArray();
        foreach (var value in values)
        {
            array.Add(new OpenApiString(value));
        }
        return array;
    }
#endif

    private static JsonObject? TrimNulls(JsonNode? node)
    {
        if (node is not JsonObject obj)
            return null;

        var trimmed = new JsonObject();
        foreach (var prop in obj)
        {
            switch (prop.Value)
            {
                case JsonArray { Count: 0 }:
                case null:
                    continue;
                default:
                    trimmed[prop.Key] = prop.Value;
                    break;
            }
        }
        return trimmed;
    }
}
