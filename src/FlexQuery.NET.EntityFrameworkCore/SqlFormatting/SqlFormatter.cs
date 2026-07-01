namespace FlexQuery.NET.EntityFrameworkCore.SqlFormatting;

internal static class SqlFormatter
{
    private const string Indent = "    ";
    private const string NewLine = "\n";

    private static readonly string[] ClauseKeywords =
    [
        "SELECT", "FROM", "WHERE", "GROUP BY", "HAVING", "ORDER BY",
        "LIMIT", "OFFSET", "LEFT JOIN", "INNER JOIN", "LEFT OUTER JOIN",
        "ON", "WITH", "UNION", "UNION ALL", "AS"
    ];

    public static string Format(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return sql ?? string.Empty;

        var tokens = new Tokenizer(sql);
        var output = new System.Text.StringBuilder();
        int indent = 0;
        bool afterNewline = true;
        bool afterClause = false;
        bool selectList = false;

        while (tokens.TryRead(out var token))
        {
            string text = token.Text;
            string upper = text.ToUpperInvariant();

            if (token.Kind == TokenKind.Comment)
            {
                if (output.Length > 0 && !afterNewline)
                    output.Append(NewLine);
                output.Append(text);
                afterNewline = true;
                afterClause = false;
                continue;
            }

            if (token.Kind == TokenKind.Keyword)
            {
                bool isClause = ClauseKeywords.Contains(upper);

                if (isClause)
                {
                    if (!afterNewline)
                        output.Append(NewLine);
                    output.Append(' ', indent * 4);
                    output.Append(upper);
                    afterNewline = false;
                    afterClause = true;

                    if (upper == "SELECT")
                        selectList = true;
                    else if (upper is "FROM" or "WHERE" or "GROUP BY" or "HAVING"
                                          or "ORDER BY" or "LIMIT" or "OFFSET"
                                          or "LEFT JOIN" or "INNER JOIN" or "LEFT OUTER JOIN"
                                          or "ON" or "WITH" or "UNION" or "UNION ALL")
                    {
                        selectList = false;
                    }
                    continue;
                }

                if (upper is "CASE" or "WHEN" or "THEN" or "ELSE" or "END")
                {
                    AppendWithSpace(output, text, ref afterNewline);
                    afterClause = false;
                    continue;
                }
            }

            if (token.Kind == TokenKind.Comma && selectList)
            {
                output.Append(text);
                afterClause = true;
                afterNewline = false;
                continue;
            }

            if (afterClause)
            {
                output.Append(NewLine);
                output.Append(' ', (indent + 1) * 4);
                output.Append(text);
                afterNewline = false;
                afterClause = false;
                continue;
            }

            if (token.Kind == TokenKind.OpenParen)
            {
                if (!afterNewline)
                    output.Append(' ');
                output.Append(text);
                indent++;
                afterNewline = false;
                continue;
            }

            if (token.Kind == TokenKind.CloseParen)
            {
                indent = Math.Max(0, indent - 1);
                if (!afterNewline)
                    output.Append(' ');
                output.Append(text);
                afterNewline = false;
                continue;
            }

            AppendWithSpace(output, text, ref afterNewline);
        }

        if (!afterNewline)
            output.Append(NewLine);

        return output.ToString();
    }

    private static void AppendWithSpace(System.Text.StringBuilder sb, string text, ref bool afterNewline)
    {
        if (sb.Length == 0)
        {
            sb.Append(text);
            afterNewline = false;
            return;
        }

        if (afterNewline)
        {
            sb.Append(text);
            afterNewline = false;
            return;
        }

        char last = sb[^1];
        if (last is ' ' or '\t' or '(' or ',')
            sb.Append(text);
        else if (text is "." or "[" or "]" or "(" or ")" or ",")
            sb.Append(text);
        else if (last is '.' or '[' or ']')
            sb.Append(text);
        else
            sb.Append(' ').Append(text);
    }

    private enum TokenKind
    {
        Keyword,
        Identifier,
        String,
        Parameter,
        Comment,
        Comma,
        OpenParen,
        CloseParen,
        Other
    }

    private ref struct Tokenizer
    {
        private readonly string _sql;
        private int _position;

        public Tokenizer(string sql)
        {
            _sql = sql;
            _position = 0;
        }

        public bool TryRead(out Token token)
        {
            token = default;

            if (_position >= _sql.Length)
                return false;

            char c = _sql[_position];

            if (c == '\'' || c == '"')
            {
                ReadString(c, out token);
                return true;
            }

            if (c == '-' && Peek() == '-')
            {
                ReadLineComment(out token);
                return true;
            }

            if (c == '/' && Peek() == '*')
            {
                ReadBlockComment(out token);
                return true;
            }

            if (c == '@')
            {
                ReadParameter(out token);
                return true;
            }

            if (c == '(')
            {
                token = new Token(TokenKind.OpenParen, "(");
                _position++;
                return true;
            }

            if (c == ')')
            {
                token = new Token(TokenKind.CloseParen, ")");
                _position++;
                return true;
            }

            if (c == ',')
            {
                token = new Token(TokenKind.Comma, ",");
                _position++;
                return true;
            }

            if (char.IsLetter(c) || c == '_')
            {
                ReadIdentifier(out token);
                return true;
            }

            if (char.IsWhiteSpace(c))
            {
                SkipWhiteSpace();
                return TryRead(out token);
            }

            token = new Token(TokenKind.Other, c.ToString());
            _position++;
            return true;
        }

        private char Peek()
        {
            int next = _position + 1;
            return next < _sql.Length ? _sql[next] : '\0';
        }

        private void ReadString(char quote, out Token token)
        {
            int start = _position;
            _position++;

            while (_position < _sql.Length)
            {
                char ch = _sql[_position];
                if (ch == quote)
                {
                    _position++;
                    if (_position < _sql.Length && _sql[_position] == quote)
                    {
                        _position++;
                        continue;
                    }
                    break;
                }
                _position++;
            }

            token = new Token(TokenKind.String, _sql[start.._position]);
        }

        private void ReadLineComment(out Token token)
        {
            int start = _position;
            while (_position < _sql.Length && _sql[_position] != '\n')
                _position++;

            token = new Token(TokenKind.Comment, _sql[start.._position]);
        }

        private void ReadBlockComment(out Token token)
        {
            int start = _position;
            _position += 2;
            while (_position + 1 < _sql.Length)
            {
                if (_sql[_position] == '*' && _sql[_position + 1] == '/')
                {
                    _position += 2;
                    break;
                }
                _position++;
            }

            token = new Token(TokenKind.Comment, _sql[start.._position]);
        }

        private void ReadParameter(out Token token)
        {
            int start = _position;
            _position++;
            while (_position < _sql.Length && (char.IsLetterOrDigit(_sql[_position]) || _sql[_position] == '_'))
                _position++;

            token = new Token(TokenKind.Parameter, _sql[start.._position]);
        }

        private void ReadIdentifier(out Token token)
        {
            int start = _position;
            while (_position < _sql.Length && (char.IsLetterOrDigit(_sql[_position]) || _sql[_position] == '_'))
                _position++;

            string first = _sql[start.._position];
            string firstUpper = first.ToUpperInvariant();

            if (ClauseKeywords.Contains(firstUpper))
            {
                token = new Token(TokenKind.Keyword, first);
                return;
            }

            int savePos = _position;
            SkipWhiteSpace(ref savePos);

            if (savePos < _sql.Length && char.IsLetter(_sql[savePos]))
            {
                int wordStart = savePos;
                while (savePos < _sql.Length && (char.IsLetterOrDigit(_sql[savePos]) || _sql[savePos] == '_'))
                    savePos++;

                string second = _sql[wordStart..savePos];
                string combined = firstUpper + " " + second.ToUpperInvariant();

                if (ClauseKeywords.Contains(combined))
                {
                    _position = savePos;
                    token = new Token(TokenKind.Keyword, combined);
                    return;
                }
            }

            token = new Token(TokenKind.Identifier, first);
        }

        private void SkipWhiteSpace()
        {
            while (_position < _sql.Length && char.IsWhiteSpace(_sql[_position]))
                _position++;
        }

        private void SkipWhiteSpace(ref int pos)
        {
            while (pos < _sql.Length && char.IsWhiteSpace(_sql[pos]))
                pos++;
        }
    }

    private readonly record struct Token(TokenKind Kind, string Text);
}
