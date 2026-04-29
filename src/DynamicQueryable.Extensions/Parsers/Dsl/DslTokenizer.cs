namespace DynamicQueryable.Parsers.Dsl;

/// <summary>Tokenizes the filter DSL into parser-friendly tokens.</summary>
public sealed class DslTokenizer
{
    private readonly string _source;
    private int _position;
    private int _colonCount;
    private bool _valueMode;

    /// <summary>Creates a tokenizer for the supplied DSL string.</summary>
    public DslTokenizer(string source)
    {
        _source = source ?? string.Empty;
    }

    /// <summary>Tokenizes the full DSL string.</summary>
    public IReadOnlyList<DslToken> Tokenize()
    {
        var tokens = new List<DslToken>();

        while (_position < _source.Length)
        {
            if (_valueMode)
            {
                tokens.Add(ReadValue());
                _valueMode = false;
                _colonCount = 0;
                continue;
            }

            var current = _source[_position];
            if (char.IsWhiteSpace(current))
            {
                _position++;
                continue;
            }

            var start = _position;
            switch (current)
            {
                case ':':
                    tokens.Add(new DslToken(DslTokenKind.Colon, ":", start));
                    _position++;
                    _colonCount++;
                    if (_colonCount == 2)
                    {
                        _valueMode = true;
                    }
                    break;
                case '&':
                    tokens.Add(new DslToken(DslTokenKind.And, "&", start));
                    _position++;
                    _colonCount = 0;
                    break;
                case '|':
                    tokens.Add(new DslToken(DslTokenKind.Or, "|", start));
                    _position++;
                    _colonCount = 0;
                    break;
                case '(':
                    tokens.Add(new DslToken(DslTokenKind.OpenParen, "(", start));
                    _position++;
                    _colonCount = 0;
                    break;
                case ')':
                    tokens.Add(new DslToken(DslTokenKind.CloseParen, ")", start));
                    _position++;
                    _colonCount = 0;
                    break;
                case '"':
                case '\'':
                    tokens.Add(ReadQuoted(current));
                    break;
                default:
                    tokens.Add(ReadIdentifier());
                    break;
            }
        }

        tokens.Add(new DslToken(DslTokenKind.End, string.Empty, _position));
        return tokens;
    }

    private DslToken ReadValue()
    {
        var start = _position;
        var value = new List<char>();

        while (_position < _source.Length && char.IsWhiteSpace(_source[_position]))
        {
            _position++;
        }

        if (_position >= _source.Length)
        {
            throw new DslParseException($"Missing DSL value at position {start}.");
        }

        var current = _source[_position];
        if (current is '"' or '\'')
        {
            return ReadQuoted(current);
        }

        while (_position < _source.Length)
        {
            current = _source[_position];
            if (current is '&' or '|' or ')' or '(')
            {
                break;
            }

            if (current == '\\' && _position + 1 < _source.Length)
            {
                _position++;
                value.Add(_source[_position]);
                _position++;
                continue;
            }

            value.Add(current);
            _position++;
        }

        var s = new string(value.ToArray()).Trim();
        if (s.Length == 0)
        {
            throw new DslParseException($"Missing DSL value at position {start}.");
        }

        return new DslToken(DslTokenKind.Identifier, s, start);
    }

    private DslToken ReadIdentifier()
    {
        var start = _position;
        var value = new List<char>();

        while (_position < _source.Length)
        {
            var current = _source[_position];
            if (current is ':' or '&' or '|' or '(' or ')' || char.IsWhiteSpace(current))
                break;

            if (current == '\\' && _position + 1 < _source.Length)
            {
                _position++;
                value.Add(_source[_position]);
                _position++;
                continue;
            }

            value.Add(current);
            _position++;
        }

        if (value.Count == 0)
            throw new DslParseException($"Unexpected character '{_source[_position]}' at position {_position}.");

        return new DslToken(DslTokenKind.Identifier, new string(value.ToArray()), start);
    }

    private DslToken ReadQuoted(char quote)
    {
        var start = _position;
        var value = new List<char>();
        _position++;

        while (_position < _source.Length)
        {
            var current = _source[_position];
            if (current == '\\' && _position + 1 < _source.Length)
            {
                _position++;
                value.Add(_source[_position]);
                _position++;
                continue;
            }

            if (current == quote)
            {
                _position++;
                return new DslToken(DslTokenKind.Identifier, new string(value.ToArray()), start);
            }

            value.Add(current);
            _position++;
        }

        throw new DslParseException($"Unterminated quoted value at position {start}.");
    }
}
