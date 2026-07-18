namespace FlexQuery.NET.Parsers.Fql;

internal sealed class FqlAstParser
{
    private readonly IReadOnlyList<FqlToken> _tokens;
    private int _position;

    public FqlAstParser(IReadOnlyList<FqlToken> tokens)
    {
        _tokens = tokens;
    }

    private FqlAstNode Parse()
    {
        var node = ParseOr();
        Expect(FqlTokenType.End);
        return node;
    }

    public static FqlAstNode Parse(string source)
    {
        FqlSafetyValidator.ValidateSyntax(source);
        return new FqlAstParser(new FqlTokenizer(source).Tokenize()).Parse();
    }

    private FqlAstNode ParseOr()
    {
        var children = new List<FqlAstNode> { ParseAnd() };
        while (Match(FqlTokenType.Or))
        {
            children.Add(ParseAnd());
        }

        return children.Count == 1
            ? children[0]
            : new FqlLogicalNode("or", Flatten("or", children));
    }

    private FqlAstNode ParseAnd()
    {
        var children = new List<FqlAstNode> { ParseFactor() };
        while (Match(FqlTokenType.And))
        {
            children.Add(ParseFactor());
        }

        return children.Count == 1
            ? children[0]
            : new FqlLogicalNode("and", Flatten("and", children));
    }

    private FqlAstNode ParseFactor()
    {
        if (Match(FqlTokenType.OpenParen))
        {
            var node = ParseOr();
            Expect(FqlTokenType.CloseParen);
            return node;
        }

        return ParseIdentifierLed();
    }

    private FqlAstNode ParseIdentifierLed()
    {
        var segments = new List<string>();
        segments.Add(Expect(FqlTokenType.Identifier).Value);

        while (Current.Kind == FqlTokenType.Dot)
        {
            var nextKind = PeekAhead(1)?.Kind;

            if (nextKind == FqlTokenType.Any || nextKind == FqlTokenType.All)
            {
                _position++;
                var quantifier = Current.Kind == FqlTokenType.Any ? "any" : "all";
                _position++;

                Expect(FqlTokenType.OpenParen);
                var innerFilter = ParseOr();
                Expect(FqlTokenType.CloseParen);

                return new FqlCollectionNode(
                    string.Join(".", segments),
                    quantifier,
                    innerFilter);
            }

            _position++;

            if (Current.Kind == FqlTokenType.Identifier)
            {
                segments.Add(_tokens[_position++].Value);
            }
            else
            {
                _position--;
                break;
            }
        }

        if (Current.Kind == FqlTokenType.OpenBracket)
        {
            _position++;
            var innerFilter = ParseOr();
            Expect(FqlTokenType.CloseBracket);

            return new FqlCollectionNode(
                string.Join(".", segments),
                "any",
                innerFilter);
        }

        var field = string.Join(".", segments);
        FqlSafetyValidator.ValidateField(field, Current.Position);
        return ParseConditionWithField(field);
    }

    private FqlConditionNode ParseConditionWithField(string field)
    {
        var op = ParseOperator();

        if (op is "isnull" or "isnotnull")
        {
            return new FqlConditionNode(field, op, Array.Empty<string>());
        }

        if (op is "any" or "all")
        {
            var nestedField = ParseDottedIdentifier();
            var nestedOp = ParseOperator();
            var nestedValues = ParseValue(nestedOp);

            var valueStr = $"{nestedField}:{nestedOp}:{string.Join(",", nestedValues)}";
            return new FqlConditionNode(field, op, new[] { valueStr });
        }

        if (op == "count")
        {
            var nestedOp = ParseOperator();
            var nestedVal = ParseSingleValue();

            var valueStr = $"{nestedOp}:{nestedVal}";
            return new FqlConditionNode(field, op, new[] { valueStr });
        }

        if (op == "between")
        {
            var val1 = ParseSingleValue();
            Expect(FqlTokenType.And);
            var val2 = ParseSingleValue();

            return new FqlConditionNode(field, op, new[] { val1, val2 });
        }

        var values = ParseValue(op);
        return new FqlConditionNode(field, op, values);
    }

    private string ParseOperator()
    {
        if (Match(FqlTokenType.Eq)) return "eq";
        if (Match(FqlTokenType.Neq)) return "neq";
        if (Match(FqlTokenType.Gte)) return "gte";
        if (Match(FqlTokenType.Gt)) return "gt";
        if (Match(FqlTokenType.Lte)) return "lte";
        if (Match(FqlTokenType.Lt)) return "lt";
        if (Match(FqlTokenType.Contains)) return "contains";
        if (Match(FqlTokenType.Like)) return "like";
        if (Match(FqlTokenType.StartsWith)) return "startswith";
        if (Match(FqlTokenType.EndsWith)) return "endswith";

        if (Match(FqlTokenType.Is))
        {
            if (Match(FqlTokenType.Not))
            {
                Expect(FqlTokenType.Null);
                return "isnotnull";
            }
            Expect(FqlTokenType.Null);
            return "isnull";
        }

        if (Match(FqlTokenType.Between)) return "between";
        if (Match(FqlTokenType.Any)) return "any";
        if (Match(FqlTokenType.All)) return "all";
        if (Match(FqlTokenType.Count)) return "count";
        if (Match(FqlTokenType.In)) return "in";

        if (Match(FqlTokenType.Not))
        {
            Expect(FqlTokenType.In);
            return "notin";
        }

        throw new FqlParseException(
            $"Expected operator but found {Current.Value}.",
            position: Current.Position,
            expected: "operator",
            found: Current.Value);
    }

    private IReadOnlyList<string> ParseValue(string op)
    {
        if (op is "in" or "notin")
        {
            Expect(FqlTokenType.OpenParen);

            var values = new List<string> { ParseSingleValue() };
            while (Match(FqlTokenType.Comma))
            {
                values.Add(ParseSingleValue());
            }

            Expect(FqlTokenType.CloseParen);

            if (values.Count == 0)
                throw new FqlParseException(
                    "IN list cannot be empty.",
                    position: Current.Position);

            return values;
        }

        return [ParseSingleValue()];
    }

    private string ParseSingleValue()
    {
        if (Current.Kind is FqlTokenType.String or FqlTokenType.Number)
        {
            return _tokens[_position++].Value;
        }

        if (Current.Kind == FqlTokenType.Identifier)
        {
            return ParseDottedIdentifier();
        }

        throw new FqlParseException(
            $"Expected value but found {Current.Kind}.",
            position: Current.Position,
            expected: "value",
            found: Current.Kind.ToString());
    }

    private string ParseDottedIdentifier()
    {
        var first = Expect(FqlTokenType.Identifier).Value;
        var sb = new System.Text.StringBuilder(first);

        while (Current.Kind == FqlTokenType.Dot && PeekAhead(1)?.Kind == FqlTokenType.Identifier)
        {
            _position++;
            sb.Append('.');
            sb.Append(_tokens[_position++].Value);
        }

        return sb.ToString();
    }

    private static IReadOnlyList<FqlAstNode> Flatten(string logic, IEnumerable<FqlAstNode> children)
    {
        var result = new List<FqlAstNode>();
        foreach (var child in children)
        {
            if (child is FqlLogicalNode logical
                && logical.Logic.Equals(logic, StringComparison.OrdinalIgnoreCase))
            {
                result.AddRange(logical.Children);
                continue;
            }

            result.Add(child);
        }

        return result;
    }

    private bool Match(FqlTokenType kind)
    {
        if (Current.Kind != kind) return false;
        _position++;
        return true;
    }

    private FqlToken Expect(FqlTokenType kind)
    {
        if (Current.Kind == kind)
            return _tokens[_position++];

        throw new FqlParseException(
            $"Expected {kind} but found {Current.Kind}.",
            position: Current.Position,
            expected: kind.ToString(),
            found: Current.Kind.ToString());
    }

    private FqlToken? PeekAhead(int offset)
    {
        var idx = _position + offset;
        return idx < _tokens.Count ? _tokens[idx] : null;
    }

    private FqlToken Current => _tokens[Math.Min(_position, _tokens.Count - 1)];
}
