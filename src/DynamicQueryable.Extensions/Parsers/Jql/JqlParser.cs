namespace DynamicQueryable.Parsers.Jql;

/// <summary>
/// Parses JQL-lite tokens into an AST using AND precedence over OR.
///
/// <para>Supported scoped collection filter syntaxes:</para>
/// <list type="bullet">
///   <item><c>orders.any(status = Cancelled AND orderItems.any(id = 101))</c></item>
///   <item><c>orders.all(status = Active)</c></item>
///   <item><c>orders[status = Cancelled AND total &gt; 500]</c> (shorthand for <c>any</c>)</item>
/// </list>
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

    // ── Expression grammar (OR > AND > Factor) ───────────────────────────

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
        // Grouped expression: ( ... )
        if (Match(JqlTokenKind.OpenParen))
        {
            var node = ParseOr();
            Expect(JqlTokenKind.CloseParen);
            return node;
        }

        // An identifier at the start – could be a plain field condition,
        // a dot-qualified path, a scoped collection node, or a legacy ANY/ALL/COUNT.
        return ParseIdentifierLed();
    }

    // ── Identifier-led expressions ────────────────────────────────────────

    /// <summary>
    /// Reads one or more dot-separated identifier segments, then decides
    /// whether the overall expression is:
    /// <list type="bullet">
    ///   <item>A scoped collection filter: <c>coll.any(expr)</c> / <c>coll.all(expr)</c></item>
    ///   <item>A bracket-scoped filter:    <c>coll[expr]</c></item>
    ///   <item>A plain condition on a (possibly dot-notated) field.</item>
    /// </list>
    /// </summary>
    private JqlAstNode ParseIdentifierLed()
    {
        // Collect leading identifier segments separated by dots.
        // We speculatively read segments and check what follows each dot.
        var segments = new List<string>();
        segments.Add(Expect(JqlTokenKind.Identifier).Value);

        while (Current.Kind == JqlTokenKind.Dot)
        {
            // Peek at what comes after the dot.
            var nextKind = PeekAhead(1)?.Kind;

            // If the token after the dot is ANY or ALL, we have a scoped collection.
            if (nextKind == JqlTokenKind.Any || nextKind == JqlTokenKind.All)
            {
                // Consume the dot and the quantifier keyword.
                _position++; // consume dot
                var quantifier = Current.Kind == JqlTokenKind.Any ? "any" : "all";
                _position++; // consume ANY / ALL keyword

                Expect(JqlTokenKind.OpenParen);
                var innerFilter = ParseOr();
                Expect(JqlTokenKind.CloseParen);

                return new JqlCollectionNode(
                    string.Join(".", segments),
                    quantifier,
                    innerFilter);
            }

            // Otherwise it's another path segment – consume dot and next identifier.
            _position++; // consume dot

            if (Current.Kind == JqlTokenKind.Identifier)
            {
                segments.Add(_tokens[_position++].Value);
            }
            else
            {
                // Dangling dot – put back and stop.
                _position--;
                break;
            }
        }

        // Bracket-scoped shorthand: collection[expr]  => collection.any(expr)
        if (Current.Kind == JqlTokenKind.OpenBracket)
        {
            _position++; // consume '['
            var innerFilter = ParseOr();
            Expect(JqlTokenKind.CloseBracket);

            return new JqlCollectionNode(
                string.Join(".", segments),
                "any",
                innerFilter);
        }

        // Plain condition: the dot-joined segments form the field path.
        var field = string.Join(".", segments);
        JqlSafetyValidator.ValidateField(field, Current.Position);
        return ParseConditionWithField(field);
    }

    // ── Condition parsing (field already consumed) ────────────────────────

    private JqlConditionNode ParseConditionWithField(string field)
    {
        var op = ParseOperator();

        if (op is "isnull" or "isnotnull")
        {
            return new JqlConditionNode(field, op, Array.Empty<string>());
        }

        // Legacy flat ANY / ALL: "orders ANY total > 1000"
        if (op is "any" or "all")
        {
            var nestedField = ParseDottedIdentifier();
            var nestedOp    = ParseOperator();
            var nestedValues = ParseValue(nestedOp);

            var valueStr = $"{nestedField}:{nestedOp}:{string.Join(",", nestedValues)}";
            return new JqlConditionNode(field, op, new[] { valueStr });
        }

        if (op == "count")
        {
            var nestedOp  = ParseOperator();
            var nestedVal = ParseSingleValue();

            var valueStr = $"{nestedOp}:{nestedVal}";
            return new JqlConditionNode(field, op, new[] { valueStr });
        }

        if (op == "between")
        {
            var val1 = ParseSingleValue();
            Expect(JqlTokenKind.And);
            var val2 = ParseSingleValue();

            return new JqlConditionNode(field, op, new[] { val1, val2 });
        }

        var values = ParseValue(op);
        return new JqlConditionNode(field, op, values);
    }

    // ── Operator parsing ──────────────────────────────────────────────────

    private string ParseOperator()
    {
        if (Match(JqlTokenKind.Eq))       return "eq";
        if (Match(JqlTokenKind.Neq))      return "neq";
        if (Match(JqlTokenKind.Gte))      return "gte";
        if (Match(JqlTokenKind.Gt))       return "gt";
        if (Match(JqlTokenKind.Lte))      return "lte";
        if (Match(JqlTokenKind.Lt))       return "lt";
        if (Match(JqlTokenKind.Contains)) return "contains";
        if (Match(JqlTokenKind.Like))     return "like";
        if (Match(JqlTokenKind.StartsWith)) return "startswith";
        if (Match(JqlTokenKind.EndsWith))   return "endswith";

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
        if (Match(JqlTokenKind.Any))     return "any";
        if (Match(JqlTokenKind.All))     return "all";
        if (Match(JqlTokenKind.Count))   return "count";
        if (Match(JqlTokenKind.In))      return "in";

        if (Match(JqlTokenKind.Not))
        {
            Expect(JqlTokenKind.In);
            return "notin";
        }

        throw new JqlParseException(
            $"Expected operator at position {Current.Position}, but found {Current.Kind}.");
    }

    // ── Value parsing ─────────────────────────────────────────────────────

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
                throw new JqlParseException(
                    $"IN list cannot be empty at position {Current.Position}.");

            return values;
        }

        return [ParseSingleValue()];
    }

    private string ParseSingleValue()
    {
        if (Current.Kind is JqlTokenKind.String or JqlTokenKind.Number)
        {
            return _tokens[_position++].Value;
        }

        // Plain identifiers may include dot-joined segments when used as values
        // (e.g. enum values like "Active" or a bare word).
        if (Current.Kind == JqlTokenKind.Identifier)
        {
            return ParseDottedIdentifier();
        }

        throw new JqlParseException(
            $"Expected value at position {Current.Position}, but found {Current.Kind}.");
    }

    /// <summary>
    /// Reads one identifier, then greedily consumes following <c>Dot + Identifier</c>
    /// pairs, returning the reassembled dot-notation string.
    /// </summary>
    private string ParseDottedIdentifier()
    {
        var first = Expect(JqlTokenKind.Identifier).Value;
        var sb = new System.Text.StringBuilder(first);

        while (Current.Kind == JqlTokenKind.Dot && PeekAhead(1)?.Kind == JqlTokenKind.Identifier)
        {
            _position++; // dot
            sb.Append('.');
            sb.Append(_tokens[_position++].Value);
        }

        return sb.ToString();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

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

    private JqlToken? PeekAhead(int offset)
    {
        var idx = _position + offset;
        return idx < _tokens.Count ? _tokens[idx] : null;
    }

    private JqlToken Current => _tokens[Math.Min(_position, _tokens.Count - 1)];
}
