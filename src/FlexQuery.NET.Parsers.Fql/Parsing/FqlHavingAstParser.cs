using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Parsers.Fql;

/// <summary>
/// Recursive-descent parser for FQL HAVING expressions.
/// Grammar:
///   expression ::= or
///   or         ::= and ('OR' and)*
///   and        ::= factor ('AND' factor)*
///   factor     ::= '(' expression ')' | aggregateCondition
///   aggregateCondition ::= Function '(' Field? ')' Operator Value
/// </summary>
internal sealed class FqlHavingAstParser(IReadOnlyList<FqlToken> tokens)
{
    private int _position;

    public static FqlAstNode Parse(string source)
    {
        var tokens = new FqlTokenizer(source).Tokenize();
        var parser = new FqlHavingAstParser(tokens);
        var result = parser.ParseExpression();

        if (parser.Current.Kind != FqlTokenType.End)
            throw new FqlParseException(
                "Unexpected token after HAVING expression.",
                position: parser.Current.Position);

        return result;
    }

    private FqlToken Current => tokens[_position];

    private FqlAstNode ParseExpression()
    {
        var left = ParseOr();

        while (true)
        {
            if (Match(FqlTokenType.And))
            {
                var right = ParseOr();
                left = new FqlHavingLogicalNode(LogicOperator.And, Flatten("and", [left, right]));
            }
            else if (Match(FqlTokenType.Or))
            {
                var right = ParseOr();
                left = new FqlHavingLogicalNode(LogicOperator.Or, Flatten("or", [left, right]));
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private FqlAstNode ParseOr()
    {
        var left = ParseAnd();

        while (Match(FqlTokenType.Or))
        {
            var right = ParseAnd();
            left = new FqlHavingLogicalNode(LogicOperator.Or, Flatten("or", [left, right]));
        }

        return left;
    }

    private FqlAstNode ParseAnd()
    {
        var left = ParseFactor();

        while (Match(FqlTokenType.And))
        {
            var right = ParseFactor();
            left = new FqlHavingLogicalNode(LogicOperator.And, Flatten("and", [left, right]));
        }

        return left;
    }

    private FqlAstNode ParseFactor()
    {
        if (!Match(FqlTokenType.OpenParen)) return ParseAggregateCondition();
        
        var inner = ParseExpression();
        Expect(FqlTokenType.CloseParen);
        return new FqlHavingGroupNode(inner);

    }

    private FqlAstNode ParseAggregateCondition()
    {
        if (_position >= tokens.Count)
            throw new FqlParseException("Expected aggregate condition but reached end of HAVING expression.", position: -1);

        var (fn, fnValue) = ExpectFunctionToken();

        if (!Match(FqlTokenType.OpenParen))
            throw new FqlParseException(
                $"Expected '(' after aggregate function '{fnValue}'.",
                position: Current.Position,
                expected: "(",
                found: Current.Value);

        string? field = null;
        if (Current.Kind != FqlTokenType.CloseParen)
        {
            if (Current.Kind == FqlTokenType.Star)
            {
                throw new FqlParseException(
                    $"Unexpected '*'. Expected a field name inside aggregate function '{fnValue}'.",
                    position: Current.Position);
            }

            var fieldParts = new List<string>();
            while (true)
            {
                if (Current.Kind == FqlTokenType.Identifier)
                {
                    fieldParts.Add(Current.Value);
                    _position++;
                }
                else if (Match(FqlTokenType.Dot))
                {
                    if (Current.Kind == FqlTokenType.Identifier)
                    {
                        fieldParts.Add(Current.Value);
                        _position++;
                    }
                    else
                    {
                        throw new FqlParseException(
                            "Expected identifier after '.' in field path.",
                            position: Current.Position,
                            expected: "identifier",
                            found: Current.Value);
                    }
                }
                else
                {
                    break;
                }
            }

            if (fieldParts.Count == 0)
                throw new FqlParseException(
                    $"Expected field name inside aggregate function '{fnValue}'.",
                    position: Current.Position);

            field = string.Join('.', fieldParts);

            if (!ParserUtilities.IsValidPropertyPath(field.AsSpan()))
                throw new FqlParseException(
                    $"Invalid field '{field}' in HAVING expression.",
                    position: Current.Position);
        }

        Expect(FqlTokenType.CloseParen);

        if (Current.Kind is FqlTokenType.And or FqlTokenType.Or)
            throw new FqlParseException(
                $"Missing operator after aggregate condition '{fnValue}({field})'.",
                position: Current.Position);

        var op = ParseComparisonOperator();

        if (Current.Kind is FqlTokenType.And or FqlTokenType.Or or FqlTokenType.End or FqlTokenType.CloseParen or FqlTokenType.CloseBracket)
            throw new FqlParseException(
                "Missing value after operator in HAVING expression.",
                position: Current.Position);

        var value = ParseValue();

        return new FqlHavingConditionNode(fn, field ?? string.Empty, op, value);
    }

    private (AggregateFunction Function, string Value) ExpectFunctionToken()
    {
        var token = Current;
        if (token.Kind is FqlTokenType.Sum or FqlTokenType.Avg or FqlTokenType.Min or FqlTokenType.Max or FqlTokenType.Count)
        {
            _position++;
            var fn = AggregateFunctionConverter.Parse(token.Value.ToLowerInvariant());
            return (fn, token.Value);
        }

        throw new FqlParseException(
            "Expected aggregate function (SUM, COUNT, AVG, MIN, MAX).",
            position: token.Position,
            expected: "aggregate function",
            found: token.Value);
    }

    private string ParseComparisonOperator()
    {
        if (Match(FqlTokenType.Gte)) return "gte";
        if (Match(FqlTokenType.Lte)) return "lte";
        if (Match(FqlTokenType.Neq)) return "neq";
        if (Match(FqlTokenType.Gt)) return "gt";
        if (Match(FqlTokenType.Lt)) return "lt";
        if (Match(FqlTokenType.Eq)) return "eq";

        throw new FqlParseException(
            "Expected comparison operator (>, >=, <, <=, =, <>).",
            position: Current.Position,
            expected: "comparison operator",
            found: Current.Value);
    }

    private string ParseValue()
    {
        if (_position >= tokens.Count)
            throw new FqlParseException("Expected value but reached end of HAVING expression.", position: -1);

        var token = Current;
        _position++;

        return token.Value;
    }

    private bool Match(FqlTokenType kind)
    {
        if (Current.Kind != kind) return false;
        _position++;
        return true;
    }

    private FqlToken Expect(FqlTokenType kind)
    {
        if (Current.Kind != kind)
            throw new FqlParseException(
                $"Expected {kind} but found {Current.Kind}.",
                position: Current.Position,
                expected: kind.ToString(),
                found: Current.Kind.ToString());

        return tokens[_position++];
    }

    private static IReadOnlyList<FqlAstNode> Flatten(string logic, IReadOnlyList<FqlAstNode> children)
    {
        var result = new List<FqlAstNode>();
        foreach (var child in children)
        {
            if (child is FqlHavingLogicalNode logical && string.Equals(logical.Logic.ToKeyword(), logic, StringComparison.OrdinalIgnoreCase))
            {
                result.AddRange(logical.Children);
                continue;
            }

            result.Add(child);
        }

        return result;
    }
}
