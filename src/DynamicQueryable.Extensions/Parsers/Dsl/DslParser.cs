namespace DynamicQueryable.Parsers.Dsl;

/// <summary>Parses DSL tokens into an AST using AND precedence over OR.</summary>
public sealed class DslParser
{
    private readonly IReadOnlyList<DslToken> _tokens;
    private int _position;

    /// <summary>Creates a parser over pre-tokenized DSL input.</summary>
    public DslParser(IReadOnlyList<DslToken> tokens)
    {
        _tokens = tokens;
    }

    /// <summary>Parses the token stream into a DSL AST.</summary>
    public DslAstNode Parse()
    {
        var node = ParseOr();
        Expect(DslTokenKind.End);
        return node;
    }

    /// <summary>Tokenizes and parses a DSL string.</summary>
    public static DslAstNode Parse(string source)
    {
        DslSafetyValidator.ValidateSyntax(source);
        return new DslParser(new DslTokenizer(source).Tokenize()).Parse();
    }

    private DslAstNode ParseOr()
    {
        var children = new List<DslAstNode> { ParseAnd() };

        while (Match(DslTokenKind.Or))
        {
            children.Add(ParseAnd());
        }

        return children.Count == 1
            ? children[0]
            : new LogicalNode("or", Flatten("or", children));
    }

    private DslAstNode ParseAnd()
    {
        var children = new List<DslAstNode> { ParsePrimary() };

        while (Match(DslTokenKind.And))
        {
            children.Add(ParsePrimary());
        }

        return children.Count == 1
            ? children[0]
            : new LogicalNode("and", Flatten("and", children));
    }

    private DslAstNode ParsePrimary()
    {
        if (Match(DslTokenKind.OpenParen))
        {
            var node = ParseOr();
            Expect(DslTokenKind.CloseParen);
            return node;
        }

        return ParseCondition();
    }

    private ConditionNode ParseCondition()
    {
        var fieldToken = Expect(DslTokenKind.Identifier);
        var field = fieldToken.Value;
        DslSafetyValidator.ValidateFieldToken(field, fieldToken.Position);

        Expect(DslTokenKind.Colon);

        var opToken = Expect(DslTokenKind.Identifier);
        DslSafetyValidator.ValidateOperatorToken(opToken.Value, opToken.Position);

        var op = opToken.Value;
        if (op.Equals("isnull", StringComparison.OrdinalIgnoreCase)
            || op.Equals("isnotnull", StringComparison.OrdinalIgnoreCase)
            || op.Equals("notnull", StringComparison.OrdinalIgnoreCase))
            return new ConditionNode(field, op, value: null);

        Expect(DslTokenKind.Colon);
        var value = Expect(DslTokenKind.Identifier).Value;
        return new ConditionNode(field, op, value);
    }

    private static IReadOnlyList<DslAstNode> Flatten(string logic, IEnumerable<DslAstNode> children)
    {
        var result = new List<DslAstNode>();
        foreach (var child in children)
        {
            if (child is LogicalNode logical
                && logical.Logic.Equals(logic, StringComparison.OrdinalIgnoreCase))
            {
                result.AddRange(logical.Children);
                continue;
            }

            result.Add(child);
        }

        return result;
    }

    private bool Match(DslTokenKind kind)
    {
        if (Current.Kind != kind) return false;
        _position++;
        return true;
    }

    private DslToken Expect(DslTokenKind kind)
    {
        if (Current.Kind == kind)
            return _tokens[_position++];

        throw new DslParseException(
            $"Expected {kind} at position {Current.Position}, but found {Current.Kind}.");
    }

    private DslToken Current => _tokens[Math.Min(_position, _tokens.Count - 1)];
}
