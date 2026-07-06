using FlexQuery.NET.Models;

namespace FlexQuery.NET.Parsers.MiniOData;

internal sealed class MiniODataQueryParser : IQueryParser
{
    public QuerySyntax Syntax => QuerySyntax.MiniOData;

    public bool CanParse(FlexQueryParameters parameters)
    {
        if (parameters.RawParameters != null)
        {
            foreach (var key in parameters.RawParameters.Keys)
            {
                if (key.StartsWith("$")) return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Filter))
        {
            var f = parameters.Filter;
            if (f.Contains(" eq ", StringComparison.OrdinalIgnoreCase) ||
                f.Contains(" ne ", StringComparison.OrdinalIgnoreCase) ||
                f.Contains("contains(", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public QueryOptions Parse(FlexQueryParameters parameters)
    {
        var request = CreateRequest(parameters);
        return ODataQueryParameterParser.Parse(request);
    }

    private static MiniODataRequest CreateRequest(FlexQueryParameters parameters)
    {
        var raw = parameters.RawParameters;
        if (raw is not { Count: > 0 })
            return new MiniODataRequest
            {
                Filter = parameters.Filter,
                OrderBy = parameters.Sort,
                Select = parameters.Select,
                Expand = parameters.Include,
                Top = parameters.PageSize,
                Skip = parameters.Page.HasValue && parameters.PageSize.HasValue
                    ? (parameters.Page.Value - 1) * parameters.PageSize.Value
                    : null,
                Count = parameters.IncludeCount
            };
        var request = new MiniODataRequest
        {
            Filter = raw.TryGetValue("$filter", out var f) ? f : null,
            OrderBy = raw.TryGetValue("$orderby", out var ob) ? ob : null,
            Select = raw.TryGetValue("$select", out var s) ? s : null,
            Expand = raw.TryGetValue("$expand", out var e) ? e : null,
            Apply = raw.TryGetValue("$apply", out var a) ? a : null
        };

        if (raw.TryGetValue("$top", out var topStr) && int.TryParse(topStr, out var top))
            request.Top = top;

        if (raw.TryGetValue("$skip", out var skipStr) && int.TryParse(skipStr, out var skip))
            request.Skip = skip;

        if (raw.TryGetValue("$count", out var countStr) && bool.TryParse(countStr, out var count))
            request.Count = count;

        return request;

    }
}
