using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Parsers.Jql;

internal static class JqlIncludeParser
{
    public static List<IncludeNode> Parse(string? raw)
    {
        return IncludeParserHelper.Parse(raw, ParseJqlFilter, msg => new JqlParseException(msg));
    }

    private static FilterGroup? ParseJqlFilter(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var ast = JqlAstParser.Parse(raw);
        return JqlFilterConverter.ToFilterGroup(ast);
    }
}
