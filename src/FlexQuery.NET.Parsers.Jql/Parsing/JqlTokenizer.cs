namespace FlexQuery.NET.Parsers.Jql;

internal sealed class JqlTokenizer
{
    private readonly string _source;
    private int _position;

    public JqlTokenizer(string source)
    {
        _source = source ?? string.Empty;
    }

    public IReadOnlyList<JqlToken> Tokenize()
    {
        var tokens = new List<JqlToken>();

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
                    tokens.Add(new JqlToken(JqlTokenType.OpenParen, "(", start));
                    _position++;
                    break;
                case ')':
                    tokens.Add(new JqlToken(JqlTokenType.CloseParen, ")", start));
                    _position++;
                    break;
                case '[':
                    tokens.Add(new JqlToken(JqlTokenType.OpenBracket, "[", start));
                    _position++;
                    break;
                case ']':
                    tokens.Add(new JqlToken(JqlTokenType.CloseBracket, "]", start));
                    _position++;
                    break;
                case ',':
                    tokens.Add(new JqlToken(JqlTokenType.Comma, ",", start));
                    _position++;
                    break;
                case '.':
                    tokens.Add(new JqlToken(JqlTokenType.Dot, ".", start));
                    _position++;
                    break;
                case '"':
                case '\'':
                    tokens.Add(ReadQuoted(ch));
                    break;
                case '!':
                    if (Peek('='))
                    {
                        tokens.Add(new JqlToken(JqlTokenType.Neq, "!=", start));
                        _position += 2;
                        break;
                    }
                    throw new JqlParseException($"Unexpected character '!' at position {start}.");
                case '>':
                    if (Peek('='))
                    {
                        tokens.Add(new JqlToken(JqlTokenType.Gte, ">=", start));
                        _position += 2;
                    }
                    else
                    {
                        tokens.Add(new JqlToken(JqlTokenType.Gt, ">", start));
                        _position++;
                    }
                    break;
                case '<':
                    if (Peek('='))
                    {
                        tokens.Add(new JqlToken(JqlTokenType.Lte, "<=", start));
                        _position += 2;
                    }
                    else
                    {
                        tokens.Add(new JqlToken(JqlTokenType.Lt, "<", start));
                        _position++;
                    }
                    break;
                case '=':
                    tokens.Add(new JqlToken(JqlTokenType.Eq, "=", start));
                    _position++;
                    break;
                case '*':
                    tokens.Add(new JqlToken(JqlTokenType.Star, "*", start));
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

        tokens.Add(new JqlToken(JqlTokenType.End, string.Empty, _position));
        return tokens;
    }

    private bool Peek(char next)
        => _position + 1 < _source.Length && _source[_position + 1] == next;

    private static bool IsNumberStart(char ch)
        => char.IsDigit(ch) || ch == '-';

    private JqlToken ReadNumber()
    {
        var start = _position;
        var hasDot = false;

        if (_source[_position] == '-')
        {
            _position++;
            if (_position >= _source.Length || !char.IsDigit(_source[_position]))
                throw new JqlParseException($"Invalid number at position {start}.");
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
        return new JqlToken(JqlTokenType.Number, raw, start);
    }

    private JqlToken ReadWordOrIdentifier()
    {
        var start = _position;

        while (_position < _source.Length)
        {
            var ch = _source[_position];
            if (char.IsWhiteSpace(ch) || ch is '(' or ')' or '[' or ']' or ',' or '.' or '=' or '!' or '<' or '>')
                break;
            _position++;
        }

        if (_position == start)
            throw new JqlParseException($"Unexpected character '{_source[_position]}' at position {_position}.");

        var raw = _source[start.._position];

        if (raw.Equals("AND", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.And, raw, start);
        if (raw.Equals("OR", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.Or, raw, start);
        if (raw.Equals("IN", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.In, raw, start);
        if (raw.Equals("NOT", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.Not, raw, start);
        if (raw.Equals("CONTAINS", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.Contains, raw, start);
        if (raw.Equals("IS", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.Is, raw, start);
        if (raw.Equals("NULL", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.Null, raw, start);
        if (raw.Equals("BETWEEN", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.Between, raw, start);
        if (raw.Equals("LIKE", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.Like, raw, start);
        if (raw.Equals("STARTSWITH", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.StartsWith, raw, start);
        if (raw.Equals("ENDSWITH", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.EndsWith, raw, start);
        if (raw.Equals("ANY", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.Any, raw, start);
        if (raw.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.All, raw, start);
        if (raw.Equals("COUNT", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.Count, raw, start);
        if (raw.Equals("ASC", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.Asc, raw, start);
        if (raw.Equals("DESC", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.Desc, raw, start);
        if (raw.Equals("SUM", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.Sum, raw, start);
        if (raw.Equals("AVG", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.Avg, raw, start);
        if (raw.Equals("MIN", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.Min, raw, start);
        if (raw.Equals("MAX", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.Max, raw, start);
        if (raw.Equals("AS", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenType.As, raw, start);

        return new JqlToken(JqlTokenType.Identifier, raw, start);
    }

    private JqlToken ReadQuoted(char quote)
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
                return new JqlToken(JqlTokenType.String, value, tokenStart);
            }

            if (ch == '\\')
            {
                return ReadEscapedQuoted(quote, tokenStart, valueStart);
            }

            _position++;
        }

        throw new JqlParseException($"Unterminated quoted value at position {tokenStart}.");
    }

    private JqlToken ReadEscapedQuoted(char quote, int tokenStart, int valueStart)
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
                return new JqlToken(JqlTokenType.String, sb.ToString(), tokenStart);
            }

            sb.Append(ch);
            _position++;
        }

        throw new JqlParseException($"Unterminated quoted value at position {tokenStart}.");
    }
}
