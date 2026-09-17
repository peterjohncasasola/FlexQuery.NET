using System.Text.Json;
using System.Text.Json.Serialization;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Serialization;

/// <summary>
/// Reads/writes an <see cref="IncludeNode"/> for the JSON request-body surface, accepting
/// both forms of an include entry: a plain navigation path string (<c>"Orders"</c>) for
/// simple inclusion, or an object (<c>{ "path": "Orders", "take": 5 }</c>) for a
/// relationship-scoped options block.
/// </summary>
internal sealed class IncludeNodeJsonConverter : JsonConverter<IncludeNode>
{
    public override IncludeNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var path = reader.GetString();
            return string.IsNullOrWhiteSpace(path)
                ? null
                : new IncludeNode { Path = path.Trim() };
        }

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("An include entry must be a navigation path string or an object.");

        var dto = JsonSerializer.Deserialize<IncludeNodeDto>(ref reader, options);
        if (dto is null || string.IsNullOrWhiteSpace(dto.Path))
            throw new JsonException("An include object must carry a non-empty 'path'.");

        return new IncludeNode
        {
            Path = dto.Path.Trim(),
            Filter = dto.Filter,
            Sort = dto.Sort,
            Take = dto.Take,
            Children = dto.Children?.Where(c => c is not null).Cast<IncludeNode>().ToList() ?? []
        };
    }

    public override void Write(Utf8JsonWriter writer, IncludeNode value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, new IncludeNodeDto
        {
            Path = value.Path,
            Filter = value.Filter,
            Sort = value.Sort,
            Take = value.Take,
            Children = value.Children
        }, options);
    }

    private sealed class IncludeNodeDto
    {
        public string? Path { get; set; }
        public FilterGroup? Filter { get; set; }
        public List<SortNode>? Sort { get; set; }
        public int? Take { get; set; }
        public List<IncludeNode?>? Children { get; set; }
    }
}
