namespace DynamicQueryable.Parsers.Jql;

/// <summary>Token kinds used by the JQL-lite parser.</summary>
public enum JqlTokenKind
{
    Identifier,
    String,
    Number,

    And,
    Or,
    In,
    Not,
    Contains,

    Eq,
    Neq,
    Gt,
    Gte,
    Lt,
    Lte,

    OpenParen,
    CloseParen,
    Comma,
    End
}

/// <summary>A single token from a JQL-lite query string.</summary>
public sealed class JqlToken
{
    public JqlToken(JqlTokenKind kind, string value, int position)
    {
        Kind = kind;
        Value = value;
        Position = position;
    }

    public JqlTokenKind Kind { get; }
    public string Value { get; }
    public int Position { get; }
}

