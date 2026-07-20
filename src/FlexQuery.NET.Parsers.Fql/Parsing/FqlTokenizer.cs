namespace FlexQuery.NET.Parsers.Fql;

internal sealed class FqlTokenizer
{
    private readonly string _source;
    private int _position;

    public FqlTokenizer(string source)
    {
        _source = source ?? string.Empty;
    }

    public IReadOnlyList<FqlToken> Tokenize()
    {
        var tokens = new List<FqlToken>();

        while (_position < _source.Length)
        {
            var ch = _source[_position];
            if (char.IsWhiteSpace(ch))
            {
                _position++;
                continue;
            }

            var start = _position;
            switch (ch)
            {
                case '(':
                    tokens.Add(new FqlToken(FqlTokenType.OpenParen, "(", start));
                    _position++;
                    break;
                case ')':
                    tokens.Add(new FqlToken(FqlTokenType.CloseParen, ")", start));
                    _position++;
                    break;
                case '[':
                    tokens.Add(new FqlToken(FqlTokenType.OpenBracket, "[", start));
                    _position++;
                    break;
                case ']':
                    tokens.Add(new FqlToken(FqlTokenType.CloseBracket, "]", start));
                    _position++;
                    break;
                case ',':
                    tokens.Add(new FqlToken(FqlTokenType.Comma, ",", start));
                    _position++;
                    break;
                case ';':
                    tokens.Add(new FqlToken(FqlTokenType.Semicolon, ";", start));
                    _position++;
                    break;
                case '.':
                    tokens.Add(new FqlToken(FqlTokenType.Dot, ".", start));
                    _position++;
                    break;
                case '"':
                case '\'':
                    tokens.Add(ReadQuoted(ch));
                    break;
                case '!':
                    if (Peek('='))
                    {
                        tokens.Add(new FqlToken(FqlTokenType.Neq, "!=", start));
                        _position += 2;
                        break;
                    }
                    throw new FqlParseException("Unexpected character '!'.", position: start, found: "!");
                case '>':
                    if (Peek('='))
                    {
                        tokens.Add(new FqlToken(FqlTokenType.Gte, ">=", start));
                        _position += 2;
                    }
                    else
                    {
                        tokens.Add(new FqlToken(FqlTokenType.Gt, ">", start));
                        _position++;
                    }
                    break;
                case '<':
                    if (Peek('='))
                    {
                        tokens.Add(new FqlToken(FqlTokenType.Lte, "<=", start));
                        _position += 2;
                    }
                    else if (Peek('>'))
                    {
                        tokens.Add(new FqlToken(FqlTokenType.Neq, "<>", start));
                        _position += 2;
                    }
                    else
                    {
                        tokens.Add(new FqlToken(FqlTokenType.Lt, "<", start));
                        _position++;
                    }
                    break;
                case '=':
                    tokens.Add(new FqlToken(FqlTokenType.Eq, "=", start));
                    _position++;
                    break;
                case '*':
                    tokens.Add(new FqlToken(FqlTokenType.Star, "*", start));
                    _position++;
                    break;
                default:
                    if (IsNumberStart(ch))
                    {
                        tokens.Add(ReadNumber());
                        break;
                    }

                    tokens.Add(ReadWordOrIdentifier());
                    break;
            }
        }

        tokens.Add(new FqlToken(FqlTokenType.End, string.Empty, _position));
        return tokens;
    }

    private bool Peek(char next)
        => _position + 1 < _source.Length && _source[_position + 1] == next;

    private static bool IsNumberStart(char ch)
        => char.IsDigit(ch) || ch == '-';

    private FqlToken ReadNumber()
    {
        var start = _position;
        var hasDot = false;

        if (_source[_position] == '-')
        {
            _position++;
            if (_position >= _source.Length || !char.IsDigit(_source[_position]))
                throw new FqlParseException("Invalid number.", position: start);
        }

        while (_position < _source.Length)
        {
            var ch = _source[_position];
            if (char.IsDigit(ch))
            {
                _position++;
                continue;
            }

            if (ch == '.' && !hasDot)
            {
                hasDot = true;
                _position++;
                continue;
            }

            break;
        }

        var raw = _source[start.._position];
        return new FqlToken(FqlTokenType.Number, raw, start);
    }

    private FqlToken ReadWordOrIdentifier()
    {
        var start = _position;

        while (_position < _source.Length)
        {
            var ch = _source[_position];
            if (char.IsWhiteSpace(ch) || ch is '(' or ')' or '[' or ']' or ',' or ';' or '.' or '=' or '!' or '<' or '>')
                break;
            _position++;
        }

        if (_position == start)
            throw new FqlParseException($"Unexpected character '{_source[_position]}'.", position: _position, found: _source[_position].ToString());

        var raw = _source[start.._position];

        if (raw.Equals("AND", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.And, raw, start);
        if (raw.Equals("OR", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.Or, raw, start);
        if (raw.Equals("IN", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.In, raw, start);
        if (raw.Equals("NOT", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.Not, raw, start);
        if (raw.Equals("CONTAINS", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.Contains, raw, start);
        if (raw.Equals("IS", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.Is, raw, start);
        if (raw.Equals("NULL", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.Null, raw, start);
        if (raw.Equals("BETWEEN", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.Between, raw, start);
        if (raw.Equals("LIKE", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.Like, raw, start);
        if (raw.Equals("STARTSWITH", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.StartsWith, raw, start);
        if (raw.Equals("ENDSWITH", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.EndsWith, raw, start);
        if (raw.Equals("ANY", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.Any, raw, start);
        if (raw.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.All, raw, start);
        if (raw.Equals("COUNT", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.Count, raw, start);
        if (raw.Equals("ASC", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.Asc, raw, start);
        if (raw.Equals("DESC", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.Desc, raw, start);
        if (raw.Equals("SUM", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.Sum, raw, start);
        if (raw.Equals("AVG", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.Avg, raw, start);
        if (raw.Equals("MIN", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.Min, raw, start);
        if (raw.Equals("MAX", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.Max, raw, start);
        if (raw.Equals("AS", StringComparison.OrdinalIgnoreCase))
            return new FqlToken(FqlTokenType.As, raw, start);

        return new FqlToken(FqlTokenType.Identifier, raw, start);
    }

    private FqlToken ReadQuoted(char quote)
    {
        var tokenStart = _position;
        _position++;
        var valueStart = _position;

        while (_position < _source.Length)
        {
            var ch = _source[_position];

            if (ch == quote)
            {
                var value = _source[valueStart.._position];
                _position++;
                return new FqlToken(FqlTokenType.String, value, tokenStart);
            }

            if (ch == '\\')
            {
                return ReadEscapedQuoted(quote, tokenStart, valueStart);
            }

            _position++;
        }

        throw new FqlParseException("Unterminated quoted value.", position: tokenStart);
    }

    private FqlToken ReadEscapedQuoted(char quote, int tokenStart, int valueStart)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(_source[valueStart.._position]);

        while (_position < _source.Length)
        {
            var ch = _source[_position];
            if (ch == '\\' && _position + 1 < _source.Length)
            {
                _position++;
                sb.Append(_source[_position]);
                _position++;
                continue;
            }

            if (ch == quote)
            {
                _position++;
                return new FqlToken(FqlTokenType.String, sb.ToString(), tokenStart);
            }

            sb.Append(ch);
            _position++;
        }

        throw new FqlParseException("Unterminated quoted value.", position: tokenStart);
    }
}
