namespace FlexQuery.NET.Parsers.MiniOData.Models;

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