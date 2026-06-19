namespace FlexQuery.NET.Parsers.Dsl;

/// <summary>Token kinds used by the filter DSL parser.</summary>
public enum DslTokenKind
{
    /// <summary>A field name, operator, or raw value segment.</summary>
    Identifier,
    /// <summary>The ':' separator between condition parts.</summary>
    Colon,
    /// <summary>The '&amp;' logical AND operator.</summary>
    And,
    /// <summary>The '|' logical OR operator.</summary>
    Or,
    /// <summary>The unary '!' NOT operator.</summary>
    Not,
    /// <summary>The '(' group opener.</summary>
    OpenParen,
    /// <summary>The ')' group closer.</summary>
    CloseParen,
    /// <summary>End of input.</summary>
    End
}

/// <summary>A single token from a DSL filter string.</summary>
public sealed class DslToken
{
    /// <summary>Creates a new DSL token.</summary>
    public DslToken(DslTokenKind kind, string value, int position)
    {
        Kind = kind;
        Value = value;
        Position = position;
    }

    /// <summary>Token kind.</summary>
    public DslTokenKind Kind { get; }

    /// <summary>Raw token value.</summary>
    public string Value { get; }

    /// <summary>Zero-based character position in the source DSL.</summary>
    public int Position { get; }
}
