using System.Text.Json;
using FlexQuery.NET.OpenApi.Documentation;
using Microsoft.AspNetCore.OpenApi;

#if NET9_0
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
#else
using Microsoft.OpenApi;
#endif

namespace FlexQuery.NET.OpenApi.Transformers;

internal sealed class FlexQuerySchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (!FlexQueryDocumentationRegistry.TryGet(context.JsonTypeInfo.Type, out var doc))
            return Task.CompletedTask;

        schema.Description = doc.Description;
        SetExample(schema, doc.Example);
        return Task.CompletedTask;
    }

    private static void SetExample(OpenApiSchema schema, object? example)
    {
        if (example is null)
            return;

#if NET9_0
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(example));
        schema.Example = ConvertToOpenApiAny(doc.RootElement);
#else
        schema.Example = JsonSerializer.SerializeToNode(example);
#endif
    }

#if NET9_0
    private static IOpenApiAny ConvertToOpenApiAny(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Object => ConvertToOpenApiObject(element),
            JsonValueKind.Array => ConvertToOpenApiArray(element),
            JsonValueKind.String => new OpenApiString(element.GetString()!),
            JsonValueKind.Number => element.TryGetInt32(out var i) ? new OpenApiInteger(i)
                                 : element.TryGetInt64(out var l) ? new OpenApiLong(l)
                                 : element.TryGetDouble(out var d) ? new OpenApiDouble(d)
                                 : new OpenApiFloat((float)element.GetDouble()),
            JsonValueKind.True => new OpenApiBoolean(true),
            JsonValueKind.False => new OpenApiBoolean(false),
            JsonValueKind.Null => new OpenApiNull(),
            _ => new OpenApiString(element.GetRawText())
        };

    private static OpenApiObject ConvertToOpenApiObject(JsonElement element)
    {
        var obj = new OpenApiObject();
        foreach (var property in element.EnumerateObject())
            obj[property.Name] = ConvertToOpenApiAny(property.Value);
        return obj;
    }

    private static OpenApiArray ConvertToOpenApiArray(JsonElement element)
    {
        var arr = new OpenApiArray();
        foreach (var item in element.EnumerateArray())
            arr.Add(ConvertToOpenApiAny(item));
        return arr;
    }
#endif
}
