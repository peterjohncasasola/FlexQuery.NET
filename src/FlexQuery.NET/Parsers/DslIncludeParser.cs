using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Parsers.Dsl;

namespace FlexQuery.NET.Parsers;

internal static class DslIncludeParser
{
    public static List<IncludeNode> Parse(string? raw)
    {
        return IncludeParserHelper.Parse(raw, ParseDslFilter, msg => new DslParseException(msg));
    }

    private static FilterGroup? ParseDslFilter(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var dslAst = DslAstParser.Parse(raw);
        return DslFilterConverter.ToFilterGroup(dslAst);
    }
}
