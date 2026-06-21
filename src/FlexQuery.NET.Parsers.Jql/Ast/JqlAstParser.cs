namespace FlexQuery.NET.Parsers.Jql;

internal sealed class JqlAstParser
{
    private readonly IReadOnlyList<JqlToken> _tokens;
    private int _position;

    public JqlAstParser(IReadOnlyList<JqlToken> tokens)
    {
        _tokens = tokens;
    }

    private JqlAstNode Parse()
    {
        var node = ParseOr();
        Expect(JqlTokenType.End);
        return node;
    }

    public static JqlAstNode Parse(string source)
    {
        JqlSafetyValidator.ValidateSyntax(source);
        return new JqlAstParser(new JqlTokenizer(source).Tokenize()).Parse();
    }

    private JqlAstNode ParseOr()
    {
        var children = new List<JqlAstNode> { ParseAnd() };
        while (Match(JqlTokenType.Or))
        {
            children.Add(ParseAnd());
        }

        return children.Count == 1
            ? children[0]
            : new JqlLogicalNode("or", Flatten("or", children));
    }

    private JqlAstNode ParseAnd()
    {
        var children = new List<JqlAstNode> { ParseFactor() };
        while (Match(JqlTokenType.And))
        {
            children.Add(ParseFactor());
        }

        return children.Count == 1
            ? children[0]
            : new JqlLogicalNode("and", Flatten("and", children));
    }

    private JqlAstNode ParseFactor()
    {
        if (Match(JqlTokenType.OpenParen))
        {
            var node = ParseOr();
            Expect(JqlTokenType.CloseParen);
            return node;
        }

        return ParseIdentifierLed();
    }

    private JqlAstNode ParseIdentifierLed()
    {
        var segments = new List<string>();
        segments.Add(Expect(JqlTokenType.Identifier).Value);

        while (Current.Kind == JqlTokenType.Dot)
        {
            var nextKind = PeekAhead(1)?.Kind;

            if (nextKind == JqlTokenType.Any || nextKind == JqlTokenType.All)
            {
                _position++;
                var quantifier = Current.Kind == JqlTokenType.Any ? "any" : "all";
                _position++;

                Expect(JqlTokenType.OpenParen);
                var innerFilter = ParseOr();
                Expect(JqlTokenType.CloseParen);

                return new JqlCollectionNode(
                    string.Join(".", segments),
                    quantifier,
                    innerFilter);
            }

            _position++;

            if (Current.Kind == JqlTokenType.Identifier)
            {
                segments.Add(_tokens[_position++].Value);
            }
            else
            {
                _position--;
                break;
            }
        }

        if (Current.Kind == JqlTokenType.OpenBracket)
        {
            _position++;
            var innerFilter = ParseOr();
            Expect(JqlTokenType.CloseBracket);

            return new JqlCollectionNode(
                string.Join(".", segments),
                "any",
                innerFilter);
        }

        var field = string.Join(".", segments);
        JqlSafetyValidator.ValidateField(field, Current.Position);
        return ParseConditionWithField(field);
    }

    private JqlConditionNode ParseConditionWithField(string field)
    {
        var op = ParseOperator();

        if (op is "isnull" or "isnotnull")
        {
            return new JqlConditionNode(field, op, Array.Empty<string>());
        }

        if (op is "any" or "all")
        {
            var nestedField = ParseDottedIdentifier();
            var nestedOp = ParseOperator();
            var nestedValues = ParseValue(nestedOp);

            var valueStr = $"{nestedField}:{nestedOp}:{string.Join(",", nestedValues)}";
            return new JqlConditionNode(field, op, new[] { valueStr });
        }

        if (op == "count")
        {
            var nestedOp = ParseOperator();
            var nestedVal = ParseSingleValue();

            var valueStr = $"{nestedOp}:{nestedVal}";
            return new JqlConditionNode(field, op, new[] { valueStr });
        }

        if (op == "between")
        {
            var val1 = ParseSingleValue();
            Expect(JqlTokenType.And);
            var val2 = ParseSingleValue();

            return new JqlConditionNode(field, op, new[] { val1, val2 });
        }

        var values = ParseValue(op);
        return new JqlConditionNode(field, op, values);
    }

    private string ParseOperator()
    {
        if (Match(JqlTokenType.Eq)) return "eq";
        if (Match(JqlTokenType.Neq)) return "neq";
        if (Match(JqlTokenType.Gte)) return "gte";
        if (Match(JqlTokenType.Gt)) return "gt";
        if (Match(JqlTokenType.Lte)) return "lte";
        if (Match(JqlTokenType.Lt)) return "lt";
        if (Match(JqlTokenType.Contains)) return "contains";
        if (Match(JqlTokenType.Like)) return "like";
        if (Match(JqlTokenType.StartsWith)) return "startswith";
        if (Match(JqlTokenType.EndsWith)) return "endswith";

        if (Match(JqlTokenType.Is))
        {
            if (Match(JqlTokenType.Not))
            {
                Expect(JqlTokenType.Null);
                return "isnotnull";
            }
            Expect(JqlTokenType.Null);
            return "isnull";
        }

        if (Match(JqlTokenType.Between)) return "between";
        if (Match(JqlTokenType.Any)) return "any";
        if (Match(JqlTokenType.All)) return "all";
        if (Match(JqlTokenType.Count)) return "count";
        if (Match(JqlTokenType.In)) return "in";

        if (Match(JqlTokenType.Not))
        {
            Expect(JqlTokenType.In);
            return "notin";
        }

        throw new JqlParseException(
            $"Expected operator at position {Current.Position}, but found {Current.Kind}.");
    }

    private IReadOnlyList<string> ParseValue(string op)
    {
        if (op is "in" or "notin")
        {
            Expect(JqlTokenType.OpenParen);

            var values = new List<string> { ParseSingleValue() };
            while (Match(JqlTokenType.Comma))
            {
                values.Add(ParseSingleValue());
            }

            Expect(JqlTokenType.CloseParen);

            if (values.Count == 0)
                throw new JqlParseException(
                    $"IN list cannot be empty at position {Current.Position}.");

            return values;
        }

        return [ParseSingleValue()];
    }

    private string ParseSingleValue()
    {
        if (Current.Kind is JqlTokenType.String or JqlTokenType.Number)
        {
            return _tokens[_position++].Value;
        }

        if (Current.Kind == JqlTokenType.Identifier)
        {
            return ParseDottedIdentifier();
        }

        throw new JqlParseException(
            $"Expected value at position {Current.Position}, but found {Current.Kind}.");
    }

    private string ParseDottedIdentifier()
    {
        var first = Expect(JqlTokenType.Identifier).Value;
        var sb = new System.Text.StringBuilder(first);

        while (Current.Kind == JqlTokenType.Dot && PeekAhead(1)?.Kind == JqlTokenType.Identifier)
        {
            _position++;
            sb.Append('.');
            sb.Append(_tokens[_position++].Value);
        }

        return sb.ToString();
    }

    private static IReadOnlyList<JqlAstNode> Flatten(string logic, IEnumerable<JqlAstNode> children)
    {
        var result = new List<JqlAstNode>();
        foreach (var child in children)
        {
            if (child is JqlLogicalNode logical
                && logical.Logic.Equals(logic, StringComparison.OrdinalIgnoreCase))
            {
                result.AddRange(logical.Children);
                continue;
            }

            result.Add(child);
        }

        return result;
    }

    private bool Match(JqlTokenType kind)
    {
        if (Current.Kind != kind) return false;
        _position++;
        return true;
    }

    private JqlToken Expect(JqlTokenType kind)
    {
        if (Current.Kind == kind)
            return _tokens[_position++];

        throw new JqlParseException(
            $"Expected {kind} at position {Current.Position}, but found {Current.Kind}.");
    }

    private JqlToken? PeekAhead(int offset)
    {
        var idx = _position + offset;
        return idx < _tokens.Count ? _tokens[idx] : null;
    }

    private JqlToken Current => _tokens[Math.Min(_position, _tokens.Count - 1)];
}
