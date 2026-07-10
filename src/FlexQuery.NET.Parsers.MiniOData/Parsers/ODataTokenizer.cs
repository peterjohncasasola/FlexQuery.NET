using System.Text;
using FlexQuery.NET.Parsers.MiniOData.Models;

namespace FlexQuery.NET.Parsers.MiniOData;

/// <summary>
/// Tokenizes OData filter expressions into a stream of <see cref="ODataToken"/> instances.
/// Handles identifiers, string literals, number literals, parentheses, commas, slashes, and colons.
/// </summary>
public sealed class ODataTokenizer
{
    private readonly string _source;
    private int _position;

    /// <summary>Creates a tokenizer for the supplied OData filter string.</summary>
    public ODataTokenizer(string source)
    {
        _source = source ?? string.Empty;
    }

    /// <summary>Tokenizes the full OData filter expression.</summary>
    public IReadOnlyList<ODataToken> Tokenize()
    {
        var tokens = new List<ODataToken>();

        while (_position < _source.Length)
        {
            SkipWhitespace();
            if (_position >= _source.Length) break;

            var current = _source[_position];
            var start = _position;

            switch (current)
            {
                case '(':
                    tokens.Add(new ODataToken(ODataTokenKind.OpenParen, "(", start));
                    _position++;
                    break;
                case ')':
                    tokens.Add(new ODataToken(ODataTokenKind.CloseParen, ")", start));
                    _position++;
                    break;
                case ',':
                    tokens.Add(new ODataToken(ODataTokenKind.Comma, ",", start));
                    _position++;
                    break;
                case '/':
                    tokens.Add(new ODataToken(ODataTokenKind.Slash, "/", start));
                    _position++;
                    break;
                case ':':
                    tokens.Add(new ODataToken(ODataTokenKind.Colon, ":", start));
                    _position++;
                    break;
                case '\'':
                    tokens.Add(ReadStringLiteral());
                    break;
                default:
                    if (char.IsDigit(current) || (current == '-' && _position + 1 < _source.Length && char.IsDigit(_source[_position + 1])))
                    {
                        tokens.Add(ReadNumberLiteral());
                    }
                    else if (char.IsLetter(current) || current == '_' || current == '$')
                    {
                        tokens.Add(ReadIdentifier());
                    }
                    else
                    {
                        throw new MiniODataParseException(
                            $"Unexpected character '{current}' at position {_position}.");
                    }
                    break;
            }
        }

        tokens.Add(new ODataToken(ODataTokenKind.End, string.Empty, _position));
        return tokens;
    }

    private void SkipWhitespace()
    {
        while (_position < _source.Length && char.IsWhiteSpace(_source[_position]))
            _position++;
    }

    private ODataToken ReadStringLiteral()
    {
        var start = _position;
        _position++; // skip opening quote
        var sb = new StringBuilder();

        while (_position < _source.Length)
        {
            var current = _source[_position];

            // OData escapes single quotes by doubling them: ''
            if (current == '\'')
            {
                if (_position + 1 < _source.Length && _source[_position + 1] == '\'')
                {
                    sb.Append('\'');
                    _position += 2;
                    continue;
                }

                _position++; // skip closing quote
                return new ODataToken(ODataTokenKind.StringLiteral, sb.ToString(), start);
            }

            sb.Append(current);
            _position++;
        }

        throw new MiniODataParseException($"Unterminated string literal at position {start}.");
    }

    private ODataToken ReadNumberLiteral()
    {
        var start = _position;

        if (_source[_position] == '-') _position++;

        while (_position < _source.Length && char.IsDigit(_source[_position]))
            _position++;

        // Decimal part
        if (_position < _source.Length && _source[_position] == '.')
        {
            _position++;
            while (_position < _source.Length && char.IsDigit(_source[_position]))
                _position++;
        }

        return new ODataToken(ODataTokenKind.NumberLiteral, _source[start.._position], start);
    }

    private ODataToken ReadIdentifier()
    {
        var start = _position;

        while (_position < _source.Length)
        {
            var c = _source[_position];
            if (char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '$')
            {
                _position++;
            }
            else
            {
                break;
            }
        }

        return new ODataToken(ODataTokenKind.Identifier, _source[start.._position], start);
    }
}
