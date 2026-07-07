using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlexQuery.NET.Internal;

namespace FlexQuery.NET.Serialization;

internal static class KeysetCursorSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string? Serialize(KeysetCursor cursor)
    {
        var dto = new CursorDto { Version = 1, Values = cursor.Values.ToArray() };
        
        if (dto.Version != 1 || dto.Values is not { Length: > 0 } vals) 
            return null;
        
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public static KeysetCursor? Deserialize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            var bytes = Convert.FromBase64String(value);
            var json = Encoding.UTF8.GetString(bytes);
            var dto = JsonSerializer.Deserialize<CursorDto>(json, JsonOptions);
            if (dto?.Values is not { Length: > 0 } vals)
                return null;

            var converted = new object?[vals.Length];
            for (var i = 0; i < vals.Length; i++)
            {
                converted[i] = ConvertValue(vals[i]);
            }
            return new KeysetCursor(converted);
        }
        catch
        {
            return null;
        }
    }

    private static object? ConvertValue(object? value)
    {
        if (value is not JsonElement element)
            return value;

        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l :
                                     element.TryGetDecimal(out var d) ? d :
                                     element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            _ => element.GetRawText()
        };
    }

    private sealed class CursorDto
    {
        [JsonPropertyName("v")]
        public int Version { get; set; }
        
        [JsonPropertyName("vals")]
        public object?[]? Values { get; set; }
    }
}
