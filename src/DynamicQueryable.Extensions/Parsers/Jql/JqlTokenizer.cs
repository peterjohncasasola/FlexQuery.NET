namespace DynamicQueryable.Parsers.Jql;

/// <summary>Tokenizes a JQL-lite query string into parser-friendly tokens.</summary>
public sealed class JqlTokenizer
{
    private readonly string _source;
    private int _position;

    /// <summary>Creates a new JqlTokenizer.</summary>
    public JqlTokenizer(string source)
    {
        _source = source ?? string.Empty;
    }

    /// <summary>Tokenizes the input string.</summary>
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
                    tokens.Add(new JqlToken(JqlTokenKind.OpenParen, "(", start));
                    _position++;
                    break;
                case ')':
                    tokens.Add(new JqlToken(JqlTokenKind.CloseParen, ")", start));
                    _position++;
                    break;
                case ',':
                    tokens.Add(new JqlToken(JqlTokenKind.Comma, ",", start));
                    _position++;
                    break;
                case '"':
                case '\'':
                    tokens.Add(ReadQuoted(ch));
                    break;
                case '!':
                    if (Peek('='))
                    {
                        tokens.Add(new JqlToken(JqlTokenKind.Neq, "!=", start));
                        _position += 2;
                        break;
                    }
                    throw new JqlParseException($"Unexpected character '!' at position {start}.");
                case '>':
                    if (Peek('='))
                    {
                        tokens.Add(new JqlToken(JqlTokenKind.Gte, ">=", start));
                        _position += 2;
                    }
                    else
                    {
                        tokens.Add(new JqlToken(JqlTokenKind.Gt, ">", start));
                        _position++;
                    }
                    break;
                case '<':
                    if (Peek('='))
                    {
                        tokens.Add(new JqlToken(JqlTokenKind.Lte, "<=", start));
                        _position += 2;
                    }
                    else
                    {
                        tokens.Add(new JqlToken(JqlTokenKind.Lt, "<", start));
                        _position++;
                    }
                    break;
                case '=':
                    tokens.Add(new JqlToken(JqlTokenKind.Eq, "=", start));
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

        tokens.Add(new JqlToken(JqlTokenKind.End, string.Empty, _position));
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
        return new JqlToken(JqlTokenKind.Number, raw, start);
    }

    private JqlToken ReadWordOrIdentifier()
    {
        var start = _position;

        while (_position < _source.Length)
        {
            var ch = _source[_position];
            if (char.IsWhiteSpace(ch) || ch is '(' or ')' or ',' or '=' or '!' or '<' or '>')
                break;
            _position++;
        }

        if (_position == start)
            throw new JqlParseException($"Unexpected character '{_source[_position]}' at position {_position}.");

        var raw = _source[start.._position];

        // Keywords (case-insensitive)
        if (raw.Equals("AND", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenKind.And, raw, start);
        if (raw.Equals("OR", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenKind.Or, raw, start);
        if (raw.Equals("IN", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenKind.In, raw, start);
        if (raw.Equals("NOT", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenKind.Not, raw, start);
        if (raw.Equals("CONTAINS", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenKind.Contains, raw, start);
        if (raw.Equals("IS", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenKind.Is, raw, start);
        if (raw.Equals("NULL", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenKind.Null, raw, start);
        if (raw.Equals("BETWEEN", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenKind.Between, raw, start);
        if (raw.Equals("LIKE", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenKind.Like, raw, start);
        if (raw.Equals("STARTSWITH", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenKind.StartsWith, raw, start);
        if (raw.Equals("ENDSWITH", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenKind.EndsWith, raw, start);
        if (raw.Equals("ANY", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenKind.Any, raw, start);
        if (raw.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenKind.All, raw, start);
        if (raw.Equals("COUNT", StringComparison.OrdinalIgnoreCase))
            return new JqlToken(JqlTokenKind.Count, raw, start);

        return new JqlToken(JqlTokenKind.Identifier, raw, start);
    }

    private JqlToken ReadQuoted(char quote)
    {
        var start = _position;
        _position++; // skip quote
        var value = new List<char>();

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
                return new JqlToken(JqlTokenKind.String, new string(value.ToArray()), start);
            }

            value.Add(current);
            _position++;
        }

        throw new JqlParseException($"Unterminated quoted value at position {start}.");
    }
}

