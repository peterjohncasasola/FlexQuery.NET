using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers.MiniOData.Models;

namespace FlexQuery.NET.Parsers.MiniOData;

internal sealed class MiniODataQueryParser : IQueryParser
{
    public QuerySyntax Syntax => QuerySyntax.MiniOData;

    public QueryOptions Parse(FlexQueryParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var request = CreateRequest(parameters);
        var options = new QueryOptions();

        if (!string.IsNullOrWhiteSpace(parameters.Filter))
        {
            try
            {
                options.Filter = ODataFilterParser.Parse(parameters.Filter);
            }
            catch (MiniODataParseException ex)
            {
                throw new QueryParseException("$filter", QuerySyntax.MiniOData, parameters.Filter, ex);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Sort))
        {
            try
            {
                options.Sort = ODataOrderByParser.Parse(parameters.Sort);
            }
            catch (MiniODataParseException ex)
            {
                throw new QueryParseException("$orderby", QuerySyntax.MiniOData, parameters.Sort, ex);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Select))
        {
            try
            {
                options.Select = ODataSelectParser.Parse(parameters.Select);
            }
            catch (MiniODataParseException ex)
            {
                throw new QueryParseException("$select", QuerySyntax.MiniOData, parameters.Select, ex);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Include))
        {
            try
            {
                options.Includes = MiniODataExpandParser.Parse(parameters.Include);
            }
            catch (MiniODataParseException ex)
            {
                throw new QueryParseException("$expand", QuerySyntax.MiniOData, parameters.Include, ex);
            }
        }

        if (request.Top.HasValue)
            options.Paging.PageSize = request.Top.Value;

        if (request is { Skip: not null, Top: not null })
            options.Paging.Page = (request.Skip.Value / request.Top.Value) + 1;

        if (request.Count.HasValue)
            options.IncludeCount = request.Count.Value;

        return options;
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
            Skip = parameters is { Page: not null, PageSize: not null }
                ? (parameters.Page.Value - 1) * parameters.PageSize.Value
                : null,
            Count = parameters.IncludeCount
        };
    }
}
