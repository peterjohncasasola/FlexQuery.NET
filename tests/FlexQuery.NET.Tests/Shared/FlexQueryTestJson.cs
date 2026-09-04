using System.Text.Json;
using FlexQuery.NET.Serialization;

namespace FlexQuery.NET.Tests.Shared;

/// <summary>
/// Shared JSON options for tests that assert the serialized response shape of typed DTO queries.
/// Includes the result-surface converter so an explicit <c>select</c> exposes only the selected
/// output fields (under their aliases).
/// </summary>
public static class FlexQueryTestJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new QueryResultShapeConverterFactory() }
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
