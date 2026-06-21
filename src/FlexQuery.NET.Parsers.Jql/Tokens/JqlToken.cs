namespace FlexQuery.NET.Parsers.Jql;

internal sealed class JqlToken(JqlTokenType kind, string value, int position)
{
    public JqlTokenType Kind { get; } = kind;
    public string Value { get; } = value;
    public int Position { get; } = position;
}
