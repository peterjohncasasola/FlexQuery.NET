namespace FlexQuery.NET.Parsers.Dsl;

/// <summary>Tokenizes the filter DSL into parser-friendly tokens.</summary>
internal sealed class DslTokenizer
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
                case '!':
                    tokens.Add(new DslToken(DslTokenKind.Not, "!", start));
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

        while (_position < _source.Length && char.IsWhiteSpace(_source[_position]))
        {
            _position++;
        }

        if (_position >= _source.Length)
        {
            throw new DslParseException("Missing DSL value.", position: start);
        }

        var current = _source[_position];
        if (current is '"' or '\'')
        {
            return ReadQuoted(current);
        }

        var valueStart = _position;
        while (_position < _source.Length)
        {
            current = _source[_position];

            if (IsAtLogicalKeyword(_source, _position))
                break;

            if (current is '&' or '|' or ')' or '(')
            {
                break;
            }

            if (current == '\\')
            {
                return ReadEscapedValue(tokenStart: start, valueStart: valueStart);
            }

            _position++;
        }

        var s = _source[valueStart.._position].Trim();
        if (s.Length == 0)
        {
            throw new DslParseException("Missing DSL value.", position: start);
        }

        return new DslToken(DslTokenKind.Identifier, s, start);
    }

    private DslToken ReadEscapedValue(int tokenStart, int valueStart)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(_source[valueStart.._position]);

        while (_position < _source.Length)
        {
            var current = _source[_position];

            if (IsAtLogicalKeyword(_source, _position))
                break;

            if (current is '&' or '|' or ')' or '(')
            {
                break;
            }

            if (current == '\\' && _position + 1 < _source.Length)
            {
                _position++;
                sb.Append(_source[_position]);
                _position++;
                continue;
            }

            sb.Append(current);
            _position++;
        }

        var s = sb.ToString().Trim();
        if (s.Length == 0)
        {
            throw new DslParseException("Missing DSL value.", position: tokenStart);
        }

        return new DslToken(DslTokenKind.Identifier, s, tokenStart);
    }

    private static bool IsAtLogicalKeyword(string source, int position)
    {
        if (position >= source.Length)
            return false;

        var remaining = source.Length - position;

        if (remaining >= 3 &&
            source[position] is 'A' or 'a' &&
            source[position + 1] is 'N' or 'n' &&
            source[position + 2] is 'D' or 'd')
        {
            var after = position + 3;
            if (after >= source.Length)
                return true;
            var ch = source[after];
            return ch is ':' or '&' or '|' or '(' or ')' || char.IsWhiteSpace(ch);
        }

        if (remaining >= 2 &&
            source[position] is 'O' or 'o' &&
            source[position + 1] is 'R' or 'r')
        {
            var after = position + 2;
            if (after >= source.Length)
                return true;
            var ch = source[after];
            return ch is ':' or '&' or '|' or '(' or ')' || char.IsWhiteSpace(ch);
        }

        return false;
    }

    private DslToken ReadIdentifier()
    {
        var start = _position;

        while (_position < _source.Length)
        {
            var current = _source[_position];
            if (current is ':' or '&' or '|' or '(' or ')' || char.IsWhiteSpace(current))
                break;

            if (current == '\\')
            {
                return ReadEscapedIdentifier(start);
            }

            _position++;
        }

        if (_position == start)
            throw new DslParseException($"Unexpected character '{_source[_position]}'.", position: _position, found: _source[_position].ToString());

        var raw = _source[start.._position];
        if (raw.Equals("AND", StringComparison.OrdinalIgnoreCase))
            return new DslToken(DslTokenKind.And, raw, start);
        if (raw.Equals("OR", StringComparison.OrdinalIgnoreCase))
            return new DslToken(DslTokenKind.Or, raw, start);

        return new DslToken(DslTokenKind.Identifier, raw, start);
    }

    private DslToken ReadEscapedIdentifier(int tokenStart)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(_source[tokenStart.._position]);

        while (_position < _source.Length)
        {
            var current = _source[_position];
            if (current is ':' or '&' or '|' or '(' or ')' || char.IsWhiteSpace(current))
                break;

            if (current == '\\' && _position + 1 < _source.Length)
            {
                _position++;
                sb.Append(_source[_position]);
                _position++;
                continue;
            }

            sb.Append(current);
            _position++;
        }

        return new DslToken(DslTokenKind.Identifier, sb.ToString(), tokenStart);
    }

    private DslToken ReadQuoted(char quote)
    {
        var tokenStart = _position;
        _position++; // skip opening quote
        var valueStart = _position;

        while (_position < _source.Length)
        {
            var current = _source[_position];

            if (current == quote)
            {
                var value = _source[valueStart.._position];
                _position++;
                return new DslToken(DslTokenKind.Identifier, value, tokenStart);
            }

            if (current == '\\')
            {
                return ReadEscapedQuoted(quote, tokenStart, valueStart);
            }

            _position++;
        }

        throw new DslParseException("Unterminated quoted value.", position: tokenStart);
    }

    private DslToken ReadEscapedQuoted(char quote, int tokenStart, int valueStart)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(_source[valueStart.._position]);

        while (_position < _source.Length)
        {
            var current = _source[_position];
            if (current == '\\' && _position + 1 < _source.Length)
            {
                _position++;
                sb.Append(_source[_position]);
                _position++;
                continue;
            }

            if (current == quote)
            {
                _position++;
                return new DslToken(DslTokenKind.Identifier, sb.ToString(), tokenStart);
            }

            sb.Append(current);
            _position++;
        }

        throw new DslParseException("Unterminated quoted value.", position: tokenStart);
    }
}
