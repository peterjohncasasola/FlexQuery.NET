using FlexQuery.NET.Parsers.Dsl;

namespace FlexQuery.NET.Parsers;

internal static class DslIncludeParser
{
    public static List<string> Parse(string? raw)
        => IncludeParserHelper.Parse(raw, msg => new DslParseException(msg, position: -1));
}
