namespace FlexQuery.NET.Parsers.MiniOData.Models;

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
