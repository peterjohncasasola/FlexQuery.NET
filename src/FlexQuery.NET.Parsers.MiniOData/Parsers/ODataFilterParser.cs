using FlexQuery.NET.Constants;
using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Parsers.MiniOData;

/// <summary>
/// Parses OData-style <c>$filter</c> expressions into the unified <see cref="FilterGroup"/> AST.
/// <para>
/// Supported syntax:
/// <list type="bullet">
///   <item>Binary comparisons: <c>name eq 'john'</c>, <c>age gt 18</c></item>
///   <item>Function calls: <c>contains(name,'john')</c>, <c>startswith(name,'jo')</c>, <c>endswith(name,'hn')</c></item>
///   <item>Logical: <c>and</c>, <c>or</c>, <c>not</c></item>
///   <item>Grouping: <c>(status eq 'active' or status eq 'pending')</c></item>
///   <item>Lambda navigation: <c>orders/any(o: o/status eq 'Cancelled')</c></item>
/// </list>
/// </para>
/// </summary>
public sealed class ODataFilterParser
{
    private readonly IReadOnlyList<ODataToken> _tokens;
    private int _position;

    // OData comparison operators → FlexQuery canonical operators
    private static readonly Dictionary<string, string> ComparisonOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eq"] = FilterOperators.Equal,
        ["ne"] = FilterOperators.NotEqual,
        ["gt"] = FilterOperators.GreaterThan,
        ["ge"] = FilterOperators.GreaterThanOrEq,
        ["lt"] = FilterOperators.LessThan,
        ["le"] = FilterOperators.LessThanOrEq,
    };

    // OData function names → FlexQuery operators
    private static readonly HashSet<string> FilterFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "contains", "startswith", "endswith"
    };

    // OData lambda quantifiers
    private static readonly HashSet<string> LambdaQuantifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "any", "all"
    };

    /// <summary>Creates a parser over pre-tokenized OData filter input.</summary>
    private ODataFilterParser(IReadOnlyList<ODataToken> tokens)
    {
        _tokens = tokens;
    }

    /// <summary>Tokenizes and parses an OData $filter string into a <see cref="FilterGroup"/>.</summary>
    public static FilterGroup Parse(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return new FilterGroup();

        var tokens = new ODataTokenizer(filter).Tokenize();
        return new ODataFilterParser(tokens).ParseExpression();
    }

    /// <summary>Parses the token stream into a <see cref="FilterGroup"/>.</summary>
    private FilterGroup ParseExpression()
    {
        var group = ParseOr();
        if (Current.Kind != ODataTokenKind.End && Current.Kind != ODataTokenKind.CloseParen)
        {
            throw new MiniODataParseException(
                $"Unexpected token '{Current.Value}' at position {Current.Position}. Expected end of expression.");
        }
        return group;
    }

    private FilterGroup ParseOr()
    {
        var left = ParseAnd();

        while (IsKeyword("or"))
        {
            _position++; // consume 'or'
            var right = ParseAnd();
            left = MergeGroups(LogicOperator.Or, left, right);
        }

        return left;
    }

    private FilterGroup ParseAnd()
    {
        var left = ParsePrimary();

        while (IsKeyword("and"))
        {
            _position++; // consume 'and'
            var right = ParsePrimary();
            left = MergeGroups(LogicOperator.And, left, right);
        }

        return left;
    }

    private FilterGroup ParsePrimary()
    {
        // NOT expression
        if (IsKeyword("not"))
        {
            _position++; // consume 'not'
            var inner = ParsePrimary();
            inner.IsNegated = !inner.IsNegated;
            return inner;
        }

        // Parenthesized expression
        if (Current.Kind == ODataTokenKind.OpenParen)
        {
            _position++; // consume '('
            var inner = ParseOr();
            Expect(ODataTokenKind.CloseParen);
            return inner;
        }

        // Function call: contains(...), startswith(...), endswith(...)
        if (Current.Kind == ODataTokenKind.Identifier && FilterFunctions.Contains(Current.Value))
        {
            return ParseFunctionCall();
        }

        // Identifier — could be comparison, lambda navigation, or in/null check
        if (Current.Kind == ODataTokenKind.Identifier)
        {
            return ParseComparisonOrLambda();
        }

        throw new MiniODataParseException(
            $"Unexpected token '{Current.Value}' at position {Current.Position}.");
    }

    private FilterGroup ParseFunctionCall()
    {
        var functionName = Current.Value.ToLowerInvariant();
        _position++; // consume function name
        Expect(ODataTokenKind.OpenParen);

        var field = ParseFieldPath();
        Expect(ODataTokenKind.Comma);
        var value = ParseLiteralValue();
        Expect(ODataTokenKind.CloseParen);

        var op = functionName switch
        {
            "contains" => FilterOperators.Contains,
            "startswith" => FilterOperators.StartsWith,
            "endswith" => FilterOperators.EndsWith,
            _ => throw new MiniODataParseException($"Unsupported function '{functionName}'.")
        };

        return WrapCondition(new FilterCondition
        {
            Field = field,
            Operator = op,
            Value = value
        });
    }

    private FilterGroup ParseComparisonOrLambda()
    {
        var fieldPath = ParseFieldPath();

        // Check for lambda navigation: field/any(x: ...) or field/all(x: ...)
        if (Current.Kind == ODataTokenKind.Slash)
        {
            _position++; // consume '/'
            if (Current.Kind == ODataTokenKind.Identifier && LambdaQuantifiers.Contains(Current.Value))
            {
                return ParseLambda(fieldPath);
            }

            // It's a deeper path segment — append and continue
            fieldPath += "." + ParseFieldPath();

            if (Current.Kind == ODataTokenKind.Slash)
            {
                _position++;
                if (Current.Kind == ODataTokenKind.Identifier && LambdaQuantifiers.Contains(Current.Value))
                {
                    return ParseLambda(fieldPath);
                }
                fieldPath += "." + ParseFieldPath();
            }
        }

        // Null check operators
        if (IsKeyword("eq") && PeekNextIsKeyword("null"))
        {
            _position++; // consume 'eq'
            _position++; // consume 'null'
            return WrapCondition(new FilterCondition
            {
                Field = fieldPath,
                Operator = FilterOperators.IsNull
            });
        }

        if (IsKeyword("ne") && PeekNextIsKeyword("null"))
        {
            _position++; // consume 'ne'
            _position++; // consume 'null'
            return WrapCondition(new FilterCondition
            {
                Field = fieldPath,
                Operator = FilterOperators.IsNotNull
            });
        }

        // IN operator: field in ('a','b','c')
        if (IsKeyword("in"))
        {
            _position++; // consume 'in'
            var values = ParseInList();
            return WrapCondition(new FilterCondition
            {
                Field = fieldPath,
                Operator = FilterOperators.In,
                Value = values
            });
        }

        // Standard binary comparison: field op value
        if (Current.Kind != ODataTokenKind.Identifier || !ComparisonOperators.TryGetValue(Current.Value, out var flexOp))
        {
            throw new MiniODataParseException(
                $"Expected comparison operator at position {Current.Position}, but found '{Current.Value}'.");
        }

        _position++; // consume operator
        var val = ParseLiteralValue();

        return WrapCondition(new FilterCondition
        {
            Field = fieldPath,
            Operator = flexOp,
            Value = val
        });
    }

    private FilterGroup ParseLambda(string navigationPath)
    {
        var quantifier = Current.Value.ToLowerInvariant();
        _position++; // consume 'any'/'all'
        Expect(ODataTokenKind.OpenParen);

        FilterGroup? scopedFilter = null;

        // Check for empty lambda: any() / all()
        if (Current.Kind != ODataTokenKind.CloseParen)
        {
            // Parse lambda variable: x:
            string? lambdaVar = null;
            if (Current.Kind == ODataTokenKind.Identifier)
            {
                var savedPos = _position;
                var candidateVar = Current.Value;
                _position++;

                if (Current.Kind == ODataTokenKind.Colon)
                {
                    lambdaVar = candidateVar;
                    _position++; // consume ':'
                }
                else
                {
                    // Not a lambda variable, revert
                    _position = savedPos;
                }
            }

            // Parse the inner expression
            var innerTokens = CollectInnerTokens();
            
            // Strip lambda variable prefix from field references
            if (lambdaVar != null)
            {
                innerTokens = StripLambdaPrefix(innerTokens, lambdaVar);
            }

            var innerParser = new ODataFilterParser(innerTokens);
            scopedFilter = innerParser.ParseExpression();
        }

        Expect(ODataTokenKind.CloseParen);

        // Convert path separators: already using dots from ParseFieldPath
        return WrapCondition(new FilterCondition
        {
            Field = navigationPath,
            Operator = quantifier,
            ScopedFilter = scopedFilter
        });
    }

    private IReadOnlyList<ODataToken> CollectInnerTokens()
    {
        var tokens = new List<ODataToken>();
        var depth = 0;

        while (_position < _tokens.Count)
        {
            var token = _tokens[_position];

            if (token.Kind == ODataTokenKind.OpenParen)
                depth++;
            else if (token.Kind == ODataTokenKind.CloseParen)
            {
                if (depth == 0) break;
                depth--;
            }
            else if (token.Kind == ODataTokenKind.End)
                break;

            tokens.Add(token);
            _position++;
        }

        tokens.Add(new ODataToken(ODataTokenKind.End, string.Empty, _position));
        return tokens;
    }

    private static IReadOnlyList<ODataToken> StripLambdaPrefix(IReadOnlyList<ODataToken> tokens, string lambdaVar)
    {
        var result = new List<ODataToken>();

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            // Check for pattern: lambdaVar / fieldName
            if (token.Kind == ODataTokenKind.Identifier
                && token.Value.Equals(lambdaVar, StringComparison.OrdinalIgnoreCase)
                && i + 2 < tokens.Count
                && tokens[i + 1].Kind == ODataTokenKind.Slash
                && tokens[i + 2].Kind == ODataTokenKind.Identifier)
            {
                // Skip lambdaVar and slash, keep field
                i++; // skip slash on next iteration
                continue;
            }

            // Skip the slash that follows a stripped lambda var
            if (token.Kind == ODataTokenKind.Slash
                && i > 0
                && result.Count > 0)
            {
                var prev = tokens[i - 1];
                if (prev.Kind == ODataTokenKind.Identifier
                    && prev.Value.Equals(lambdaVar, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            result.Add(token);
        }

        return result;
    }

    private string ParseFieldPath()
    {
        var token = Expect(ODataTokenKind.Identifier);
        var path = token.Value;

        // Handle slash-separated paths: convert to dot-notation
        while (Current.Kind == ODataTokenKind.Slash
               && _position + 1 < _tokens.Count
               && _tokens[_position + 1].Kind == ODataTokenKind.Identifier
               && !LambdaQuantifiers.Contains(_tokens[_position + 1].Value))
        {
            _position++; // consume '/'
            var next = Expect(ODataTokenKind.Identifier);
            path += "." + next.Value;
        }

        return path;
    }

    private string ParseLiteralValue()
    {
        var token = Current;

        switch (token.Kind)
        {
            case ODataTokenKind.StringLiteral:
                _position++;
                return token.Value;

            case ODataTokenKind.NumberLiteral:
                _position++;
                return token.Value;

            case ODataTokenKind.Identifier when token.Value.Equals("true", StringComparison.OrdinalIgnoreCase):
                _position++;
                return "true";

            case ODataTokenKind.Identifier when token.Value.Equals("false", StringComparison.OrdinalIgnoreCase):
                _position++;
                return "false";

            case ODataTokenKind.Identifier when token.Value.Equals("null", StringComparison.OrdinalIgnoreCase):
                _position++;
                return "null";

            default:
                throw new MiniODataParseException(
                    $"Expected literal value at position {token.Position}, but found '{token.Value}'.");
        }
    }

    private string ParseInList()
    {
        Expect(ODataTokenKind.OpenParen);
        var values = new List<string>();

        while (Current.Kind != ODataTokenKind.CloseParen && Current.Kind != ODataTokenKind.End)
        {
            values.Add(ParseLiteralValue());
            if (Current.Kind == ODataTokenKind.Comma)
                _position++; // consume ','
        }

        Expect(ODataTokenKind.CloseParen);
        return string.Join(",", values);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static FilterGroup WrapCondition(FilterCondition condition)
    {
        return new FilterGroup
        {
            Logic = LogicOperator.And,
            Filters = { condition }
        };
    }

    private static FilterGroup MergeGroups(LogicOperator logic, FilterGroup left, FilterGroup right)
    {
        // If left already uses the target logic and isn't negated, absorb right into it
        if (left.Logic == logic && !left.IsNegated)
        {
            if (IsSimpleWrapper(right))
            {
                left.Filters.AddRange(right.Filters);
            }
            else
            {
                left.Groups.Add(right);
            }
            return left;
        }

        // Both are simple wrappers — create a new group at the target logic
        if (IsSimpleWrapper(left) && IsSimpleWrapper(right))
        {
            var merged = new FilterGroup { Logic = logic };
            merged.Filters.AddRange(left.Filters);
            merged.Filters.AddRange(right.Filters);
            return merged;
        }

        // General case: wrap both as subgroups
        return new FilterGroup
        {
            Logic = logic,
            Groups = { left, right }
        };

        // Helper: check if a group is a simple single-condition wrapper
        static bool IsSimpleWrapper(FilterGroup g) =>
            g is { IsNegated: false, Filters.Count: <= 1, Groups.Count: 0 };
    }

    private bool IsKeyword(string keyword)
    {
        return Current.Kind == ODataTokenKind.Identifier
               && Current.Value.Equals(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private bool PeekNextIsKeyword(string keyword)
    {
        if (_position + 1 >= _tokens.Count) return false;
        var next = _tokens[_position + 1];
        return next.Kind == ODataTokenKind.Identifier
               && next.Value.Equals(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private ODataToken Expect(ODataTokenKind kind)
    {
        if (Current.Kind == kind)
            return _tokens[_position++];

        throw new MiniODataParseException(
            $"Expected {kind} at position {Current.Position}, but found {Current.Kind} ('{Current.Value}').");
    }

    private ODataToken Current => _tokens[Math.Min(_position, _tokens.Count - 1)];
}
