namespace FlexQuery.NET.Parsers.Fql;

internal sealed class FqlToken(FqlTokenType kind, string value, int position)
{
    public FqlTokenType Kind { get; } = kind;
    public string Value { get; } = value;
    public int Position { get; } = position;
}
