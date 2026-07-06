namespace FlexQuery.NET.Parsers.Dsl;

/// <summary>A single token from a DSL filter string.</summary>
internal sealed class DslToken
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
