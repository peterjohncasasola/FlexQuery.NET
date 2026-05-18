namespace FlexQuery.NET.MiniOData.Parsers;

/// <summary>Token kinds produced by the OData tokenizer.</summary>
public enum ODataTokenKind
{
    /// <summary>Alphanumeric identifier or keyword.</summary>
    Identifier,
    /// <summary>Single-quoted string literal.</summary>
    StringLiteral,
    /// <summary>Numeric literal (integer or decimal).</summary>
    NumberLiteral,
    /// <summary>Opening parenthesis.</summary>
    OpenParen,
    /// <summary>Closing parenthesis.</summary>
    CloseParen,
    /// <summary>Comma separator.</summary>
    Comma,
    /// <summary>Forward slash (path separator).</summary>
    Slash,
    /// <summary>Colon (lambda variable separator).</summary>
    Colon,
    /// <summary>End of input.</summary>
    End
}

/// <summary>A single token from OData filter expression tokenization.</summary>
public sealed class ODataToken
{
    /// <summary>Creates a new OData token.</summary>
    public ODataToken(ODataTokenKind kind, string value, int position)
    {
        Kind = kind;
        Value = value;
        Position = position;
    }

    /// <summary>Token classification.</summary>
    public ODataTokenKind Kind { get; }

    /// <summary>Raw string value of the token.</summary>
    public string Value { get; }

    /// <summary>Character position in the source string.</summary>
    public int Position { get; }

    /// <inheritdoc />
    public override string ToString() => $"[{Kind}] '{Value}' @{Position}";
}
