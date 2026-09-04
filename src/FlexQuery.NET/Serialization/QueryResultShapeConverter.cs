using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Serialization;

/// <summary>
/// Shapes <c>QueryResult&lt;T&gt;</c> serialization so that, when an explicit
/// <c>select</c> result surface is attached (<see cref="QueryResult{T}.ResultShape"/>),
/// only the selected output fields are written (using their alias names).
/// </summary>
/// <remarks>
/// The converter is applied automatically via the <c>[JsonConverter]</c> attribute on
/// <see cref="QueryResult{T}"/>, so shaping is applied without any opt-in registration.
/// When no result surface is attached (default projection) the converter reproduces the
/// default serialization exactly (same options, naming policy, ordering, and null handling),
/// so existing output is preserved.
/// </remarks>
public sealed class QueryResultShapeConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType) return false;
        return typeToConvert.GetGenericTypeDefinition() == typeof(QueryResult<>);
    }

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var itemType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(QueryResultShapeConverter<>).MakeGenericType(itemType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

/// <inheritdoc />
public sealed class QueryResultShapeConverter<T> : JsonConverter<QueryResult<T>>
{
    /// <inheritdoc />
    public override QueryResult<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return default;
        }

        int? totalCount = null;
        int? resultCount = null;
        var page = 0;
        var pageSize = 0;
        string? nextCursorToken = null;
        Dictionary<string, Dictionary<string, object>>? aggregates = null;
        var data = new List<T>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var propertyName = reader.GetString();
            reader.Read();

            switch (propertyName?.ToLowerInvariant())
            {
                case "totalcount":
                    totalCount = reader.TokenType == JsonTokenType.Null ? null : reader.GetInt32();
                    break;
                case "resultcount":
                    resultCount = reader.TokenType == JsonTokenType.Null ? null : reader.GetInt32();
                    break;
                case "page":
                    page = reader.TokenType == JsonTokenType.Null ? 0 : reader.GetInt32();
                    break;
                case "pagesize":
                    pageSize = reader.TokenType == JsonTokenType.Null ? 0 : reader.GetInt32();
                    break;
                case "nextcursortoken":
                    nextCursorToken = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                    break;
                case "aggregates":
                    aggregates = reader.TokenType == JsonTokenType.Null
                        ? null
                        : JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(ref reader, options);
                    break;
                case "data":
                    if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                            {
                                data.Add(JsonSerializer.Deserialize<T>(ref reader, options)!);
                            }
                        }
                    }
                    else
                    {
                        reader.Skip();
                    }
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return new QueryResult<T>
        {
            TotalCount = totalCount,
            ResultCount = resultCount,
            Page = page,
            PageSize = pageSize,
            NextCursorToken = nextCursorToken,
            Aggregates = aggregates,
            Data = data
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, QueryResult<T> value, JsonSerializerOptions options)
    {
        var shape = value.ResultShape;
        var hasShape = shape is { Count: > 0 };

        // Typed DTO projections can bind raw entity collections (e.g. Customer.Orders) into
        // the response, and tracked queries or lazy-loading proxies can populate the inverse
        // navigation on those entities — producing a bidirectional object graph. Serialize
        // with ReferenceHandler.IgnoreCycles so a back-reference writes as null instead of
        // throwing a depth-32 JsonException at the host.
        var cycleSafeOptions = GetCycleSafeOptions(options);

        writer.WriteStartObject();

        WriteProperty(writer, cycleSafeOptions, nameof(QueryResult<T>.TotalCount), value.TotalCount);
        WriteProperty(writer, cycleSafeOptions, nameof(QueryResult<T>.ResultCount), value.ResultCount);
        WriteProperty(writer, cycleSafeOptions, nameof(QueryResult<T>.Page), value.Page);
        WriteProperty(writer, cycleSafeOptions, nameof(QueryResult<T>.PageSize), value.PageSize);
        WriteProperty(writer, cycleSafeOptions, nameof(QueryResult<T>.TotalPages), value.TotalPages);
        WriteProperty(writer, cycleSafeOptions, nameof(QueryResult<T>.HasNextPage), value.HasNextPage);
        WriteProperty(writer, cycleSafeOptions, nameof(QueryResult<T>.HasPreviousPage), value.HasPreviousPage);
        WriteProperty(writer, cycleSafeOptions, nameof(QueryResult<T>.Aggregates), value.Aggregates);
        if (hasShape)
            WriteProperty(writer, cycleSafeOptions, nameof(QueryResult<T>.Data), ShapeData(value.Data, shape));
        else
            WriteProperty(writer, cycleSafeOptions, nameof(QueryResult<T>.Data), value.Data);
        WriteProperty(writer, cycleSafeOptions, nameof(QueryResult<T>.NextCursorToken), value.NextCursorToken);

        writer.WriteEndObject();
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> CycleSafeOptionsCache = new();

    private static JsonSerializerOptions GetCycleSafeOptions(JsonSerializerOptions options)
    {
        // Respect explicit user intent: Preserve keeps $id/$values metadata; IgnoreCycles
        // already tolerates cycles. Only the default handler is upgraded.
        var handler = options.ReferenceHandler;
        if (handler != null
            && (ReferenceEquals(handler, ReferenceHandler.Preserve) || ReferenceEquals(handler, ReferenceHandler.IgnoreCycles)))
        {
            return options;
        }

        // One derived options instance per ambient options instance (host singleton),
        // so JsonSerializer type-info caching is amortized.
        return CycleSafeOptionsCache.GetValue(options, static o =>
            new JsonSerializerOptions(o) { ReferenceHandler = ReferenceHandler.IgnoreCycles });
    }

    private static void WriteProperty<TValue>(Utf8JsonWriter writer, JsonSerializerOptions options, string clrName, TValue value)
    {
        var jsonName = options.PropertyNamingPolicy?.ConvertName(clrName) ?? clrName;
        writer.WritePropertyName(jsonName);
        JsonSerializer.Serialize(writer, value, options);
    }

    private static List<Dictionary<string, object?>> ShapeData(IReadOnlyList<T> data, IReadOnlyList<SelectOutputField> shape)
    {
        var result = new List<Dictionary<string, object?>>(data.Count);
        foreach (var item in data)
        {
            result.Add(ShapeItem(item, shape));
        }
        return result;
    }

    private static Dictionary<string, object?> ShapeItem(T item, IReadOnlyList<SelectOutputField> shape)
    {
        var result = new Dictionary<string, object?>(shape.Count);
        if (item is null) return result;

        var type = item.GetType();
        foreach (var field in shape)
        {
            var prop = type.GetProperty(
                field.SourcePropertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            var value = prop?.GetValue(item);
            result[field.OutputName] = field.Children is { Count: > 0 }
                ? ShapeNestedValue(value, field.Children)
                : value;
        }

        return result;
    }

    /// <summary>
    /// Shapes a nested navigation value (collection element or reference) using its child
    /// output fields, so nested select trees expose only the selected child properties.
    /// </summary>
    private static object? ShapeNestedValue(object? value, IReadOnlyList<SelectOutputField> children)
    {
        if (value is null) return null;

        if (value is System.Collections.IEnumerable enumerable and not string)
        {
            var items = new List<Dictionary<string, object?>>();
            foreach (var element in enumerable)
            {
                if (element is null) continue;
                items.Add(ShapeNestedItem(element, children));
            }
            return items;
        }

        return ShapeNestedItem(value, children);
    }

    private static Dictionary<string, object?> ShapeNestedItem(object element, IReadOnlyList<SelectOutputField> children)
    {
        var dict = new Dictionary<string, object?>(children.Count);
        var elementType = element.GetType();
        foreach (var child in children)
        {
            var prop = elementType.GetProperty(
                child.SourcePropertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            var childValue = prop?.GetValue(element);
            dict[child.OutputName] = child.Children is { Count: > 0 }
                ? ShapeNestedValue(childValue, child.Children)
                : childValue;
        }
        return dict;
    }
}
