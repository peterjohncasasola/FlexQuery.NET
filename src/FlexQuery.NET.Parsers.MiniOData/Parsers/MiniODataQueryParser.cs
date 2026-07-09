using FlexQuery.NET.Models;

namespace FlexQuery.NET.Parsers.MiniOData;

internal sealed class MiniODataQueryParser : IQueryParser
{
    public QuerySyntax Syntax => QuerySyntax.MiniOData;

    public QueryOptions Parse(FlexQueryParameters parameters)
    {
        var request = CreateRequest(parameters);
        return ODataQueryParameterParser.Parse(request);
    }

    private static MiniODataRequest CreateRequest(FlexQueryParameters parameters)
    {
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
    }
}
