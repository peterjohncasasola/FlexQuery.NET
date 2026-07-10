using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Parsers.Fql;

internal static class FqlIncludeParser
{
    public static List<IncludeNode> Parse(string? raw)
    {
        return IncludeParserHelper.Parse(raw, ParseFqlFilter, msg => new FqlParseException(msg));
    }

    private static FilterGroup? ParseFqlFilter(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var ast = FqlAstParser.Parse(raw);
        return FqlFilterConverter.ToFilterGroup(ast);
    }
}
