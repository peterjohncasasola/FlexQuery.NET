namespace FlexQuery.NET.Parsers.Fql;

internal static class FqlIncludeParser
{
    public static List<string> Parse(string? raw)
        => IncludeParserHelper.Parse(raw, msg => new FqlParseException(msg));
}
