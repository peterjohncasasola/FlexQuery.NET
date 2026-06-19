using System.Text.Json;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Parsers;
internal static class JsonParser
{
    // ── JSON Filter Format ───────────────────────────────────────────────
    //  ?filter={"logic":"and","filters":[{"field":"Name","operator":"contains","value":"john"}]}

    internal static bool IsJsonFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return false;

        var span = filter.AsSpan();
        while (span.Length > 0 && char.IsWhiteSpace(span[0]))
        {
            span = span[1..];
        }

        return span.Length > 0 && span[0] == '{';
    }

    internal static QueryOptions Parse(QueryOptions options, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty(QueryOptionKeys.Select, out var selectEl))
            {
                options.SelectTree = SelectTreeBuilder.ParseJsonSelect(selectEl);
            }

            if (doc.RootElement.TryGetProperty(QueryOptionKeys.Filters, out _) || doc.RootElement.TryGetProperty(QueryOptionKeys.Logic, out _))
            {
                options.Filter = ParseJsonGroup(doc.RootElement);
            }
            else if (doc.RootElement.TryGetProperty(QueryOptionKeys.Filter, out var filterEl))
            {
                options.Filter = ParseJsonGroup(filterEl);
            }
        }
        catch { /* malformed JSON — ignore */ }

        return options;
    }

    private static FilterGroupNode ParseJsonGroup(JsonElement root)
    {
        var group = new FilterGroupNode();

        if (root.TryGetProperty(QueryOptionKeys.Logic, out var logicEl))
            group.Logic = ParserUtilities.ParseLogic(logicEl.GetString());

        if (root.TryGetProperty(QueryOptionKeys.Filters, out var filtersEl)
            && filtersEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in filtersEl.EnumerateArray())
            {
                // Nested group?
                if (item.TryGetProperty(QueryOptionKeys.Logic, out _) || item.TryGetProperty(QueryOptionKeys.Filters, out _))
                {
                    group.Children.Add(ParseJsonGroup(item));
                    continue;
                }

                var field = item.TryGetProperty(QueryOptionKeys.Field, out var f) ? f.GetString() : null;
                var op = item.TryGetProperty(QueryOptionKeys.Operator, out var o) ? o.GetString() : "eq";
                var value = item.TryGetProperty(QueryOptionKeys.Value, out var v)
                    ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText())
                    : null;

                if (!string.IsNullOrWhiteSpace(field))
                    group.Children.Add(new FilterConditionNode
                    {
                        Field = field,
                        Operator = FilterOperators.Normalize(op),
                        Value = value
                    });
            }
        }

        return group;
    }
}