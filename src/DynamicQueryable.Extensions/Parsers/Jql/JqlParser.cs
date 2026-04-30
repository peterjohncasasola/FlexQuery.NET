namespace DynamicQueryable.Parsers.Jql;

/// <summary>
/// Parses JQL-lite tokens into an AST using AND precedence over OR.
/// </summary>
public sealed class JqlParser
{
    private readonly IReadOnlyList<JqlToken> _tokens;
    private int _position;

    /// <summary>Creates a new parser with the provided tokens.</summary>
    public JqlParser(IReadOnlyList<JqlToken> tokens)
    {
        _tokens = tokens;
    }

    /// <summary>Parses the tokens into an AST.</summary>
    public JqlAstNode Parse()
    {
        var node = ParseOr();
        Expect(JqlTokenKind.End);
        return node;
    }

    /// <summary>Tokenizes and parses a JQL-lite query string.</summary>
    public static JqlAstNode Parse(string source)
    {
        JqlSafetyValidator.ValidateSyntax(source);
        return new JqlParser(new JqlTokenizer(source).Tokenize()).Parse();
    }

    private JqlAstNode ParseOr()
    {
        var children = new List<JqlAstNode> { ParseAnd() };
        while (Match(JqlTokenKind.Or))
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
        while (Match(JqlTokenKind.And))
        {
            children.Add(ParseFactor());
        }

        return children.Count == 1
            ? children[0]
            : new JqlLogicalNode("and", Flatten("and", children));
    }

    private JqlAstNode ParseFactor()
    {
        if (Match(JqlTokenKind.OpenParen))
        {
            var node = ParseOr();
            Expect(JqlTokenKind.CloseParen);
            return node;
        }

        return ParseCondition();
    }

    private JqlConditionNode ParseCondition()
    {
        var fieldToken = Expect(JqlTokenKind.Identifier);
        JqlSafetyValidator.ValidateField(fieldToken.Value, fieldToken.Position);

        var op = ParseOperator();

        if (op is "isnull" or "isnotnull")
        {
            return new JqlConditionNode(fieldToken.Value, op, Array.Empty<string>());
        }

        if (op is "any" or "all")
        {
            var nestedField = Expect(JqlTokenKind.Identifier).Value;
            var nestedOp = ParseOperator();
            var nestedValues = ParseValue(nestedOp);

            var valueStr = $"{nestedField}:{nestedOp}:{string.Join(",", nestedValues)}";
            return new JqlConditionNode(fieldToken.Value, op, new[] { valueStr });
        }

        if (op == "count")
        {
            var nestedOp = ParseOperator();
            var nestedVal = ParseSingleValue();

            var valueStr = $"{nestedOp}:{nestedVal}";
            return new JqlConditionNode(fieldToken.Value, op, new[] { valueStr });
        }

        if (op == "between")
        {
            var val1 = ParseSingleValue();
            Expect(JqlTokenKind.And);
            var val2 = ParseSingleValue();

            return new JqlConditionNode(fieldToken.Value, op, new[] { val1, val2 });
        }

        var values = ParseValue(op);

        return new JqlConditionNode(fieldToken.Value, op, values);
    }

    private string ParseOperator()
    {
        if (Match(JqlTokenKind.Eq))  return "eq";
        if (Match(JqlTokenKind.Neq)) return "neq";
        if (Match(JqlTokenKind.Gte)) return "gte";
        if (Match(JqlTokenKind.Gt))  return "gt";
        if (Match(JqlTokenKind.Lte)) return "lte";
        if (Match(JqlTokenKind.Lt))  return "lt";
        if (Match(JqlTokenKind.Contains)) return "contains";
        if (Match(JqlTokenKind.Like)) return "like";
        if (Match(JqlTokenKind.StartsWith)) return "startswith";
        if (Match(JqlTokenKind.EndsWith)) return "endswith";

        if (Match(JqlTokenKind.Is))
        {
            if (Match(JqlTokenKind.Not))
            {
                Expect(JqlTokenKind.Null);
                return "isnotnull";
            }
            Expect(JqlTokenKind.Null);
            return "isnull";
        }

        if (Match(JqlTokenKind.Between)) return "between";

        if (Match(JqlTokenKind.Any)) return "any";
        if (Match(JqlTokenKind.All)) return "all";
        if (Match(JqlTokenKind.Count)) return "count";

        if (Match(JqlTokenKind.In)) return "in";

        if (Match(JqlTokenKind.Not))
        {
            Expect(JqlTokenKind.In);
            return "notin";
        }

        throw new JqlParseException($"Expected operator at position {Current.Position}, but found {Current.Kind}.");
    }

    private IReadOnlyList<string> ParseValue(string op)
    {
        if (op is "in" or "notin")
        {
            Expect(JqlTokenKind.OpenParen);

            var values = new List<string> { ParseSingleValue() };
            while (Match(JqlTokenKind.Comma))
            {
                values.Add(ParseSingleValue());
            }

            Expect(JqlTokenKind.CloseParen);

            if (values.Count == 0)
                throw new JqlParseException($"IN list cannot be empty at position {Current.Position}.");

            return values;
        }

        return [ParseSingleValue()];
    }

    private string ParseSingleValue()
    {
        if (Current.Kind is JqlTokenKind.String or JqlTokenKind.Number or JqlTokenKind.Identifier)
        {
            return _tokens[_position++].Value;
        }

        throw new JqlParseException($"Expected value at position {Current.Position}, but found {Current.Kind}.");
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

    private bool Match(JqlTokenKind kind)
    {
        if (Current.Kind != kind) return false;
        _position++;
        return true;
    }

    private JqlToken Expect(JqlTokenKind kind)
    {
        if (Current.Kind == kind)
            return _tokens[_position++];

        throw new JqlParseException(
            $"Expected {kind} at position {Current.Position}, but found {Current.Kind}.");
    }

    private JqlToken Current => _tokens[Math.Min(_position, _tokens.Count - 1)];
}

