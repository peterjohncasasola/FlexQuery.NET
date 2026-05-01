namespace DynamicQueryable.Parsers.Jql;

/// <summary>Token kinds used by the JQL-lite parser.</summary>
public enum JqlTokenKind
{
    /// <summary>Identifier token.</summary>
    Identifier,
    /// <summary>String token.</summary>
    String,
    /// <summary>Number token.</summary>
    Number,

    /// <summary>AND logical operator.</summary>
    And,
    /// <summary>OR logical operator.</summary>
    Or,
    /// <summary>IN operator.</summary>
    In,
    /// <summary>NOT logical operator.</summary>
    Not,
    /// <summary>CONTAINS operator.</summary>
    Contains,

    /// <summary>Equality operator.</summary>
    Eq,
    /// <summary>Inequality operator.</summary>
    Neq,
    /// <summary>Greater than operator.</summary>
    Gt,
    /// <summary>Greater than or equal operator.</summary>
    Gte,
    /// <summary>Less than operator.</summary>
    Lt,
    /// <summary>Less than or equal operator.</summary>
    Lte,

    /// <summary>Open parenthesis.</summary>
    OpenParen,
    /// <summary>Close parenthesis.</summary>
    CloseParen,
    /// <summary>Open square bracket (scoped collection filter).</summary>
    OpenBracket,
    /// <summary>Close square bracket (scoped collection filter).</summary>
    CloseBracket,
    /// <summary>Comma.</summary>
    Comma,
    /// <summary>Dot separator (collection.any / collection.all).</summary>
    Dot,
    /// <summary>End of input.</summary>
    End,

    /// <summary>IS operator.</summary>
    Is,
    /// <summary>NULL token.</summary>
    Null,
    /// <summary>BETWEEN operator.</summary>
    Between,
    /// <summary>LIKE operator.</summary>
    Like,
    /// <summary>STARTSWITH operator.</summary>
    StartsWith,
    /// <summary>ENDSWITH operator.</summary>
    EndsWith,
    /// <summary>ANY collection operator.</summary>
    Any,
    /// <summary>ALL collection operator.</summary>
    All,
    /// <summary>COUNT collection operator.</summary>
    Count
}

/// <summary>A single token from a JQL-lite query string.</summary>
public sealed class JqlToken
{
    /// <summary>Creates a new JQL token.</summary>
    public JqlToken(JqlTokenKind kind, string value, int position)
    {
        Kind = kind;
        Value = value;
        Position = position;
    }

    /// <summary>The token kind.</summary>
    public JqlTokenKind Kind { get; }
    /// <summary>The string value.</summary>
    public string Value { get; }
    /// <summary>The character position in the source.</summary>
    public int Position { get; }
}

