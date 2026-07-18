using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Parsers.Dsl;
using System.Text;
using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Parsers;

/// <summary>
/// Parses HAVING clause expressions for grouped queries using native DSL syntax.
/// Supports AND, OR, and parentheses for grouping.
/// Canonical DSL syntax: aggregate-function:field:operator:value
/// Example: sum:total:gt:100 AND count:id:gte:5
/// </summary>
internal static class DslHavingParser
{
    /// <summary>
    /// Parses a HAVING clause string into a <see cref="HavingNode"/> tree.
    /// Throws <see cref="DslParseException"/> if the input is malformed.
    /// </summary>
    public static HavingNode? Parse(string? rawHaving)
    {
        if (string.IsNullOrWhiteSpace(rawHaving)) return null;

        var trimmed = rawHaving.Trim();
        if (trimmed.Length == 0) return null;

        var tokens = Tokenize(trimmed);
        var parser = new HavingTokenParser(tokens);
        var result = parser.ParseExpression();

        if (parser.CurrentToken != null)
            throw new DslParseException(
                $"Unable to parse HAVING expression '{rawHaving}'. Unexpected token at position {parser.CurrentToken.Position}.");

        return result;
    }

    private record HavingToken(string Value, int Position);

    private static List<HavingToken> Tokenize(string input)
    {
        var tokens = new List<HavingToken>();
        var sb = new StringBuilder();
        var pos = 0;

        while (pos < input.Length)
        {
            var ch = input[pos];

            if (char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0)
                {
                    tokens.Add(new HavingToken(sb.ToString(), pos - sb.Length));
                    sb.Clear();
                }
                pos++;
                continue;
            }

            switch (ch)
            {
                case '(' or ')':
                {
                    if (sb.Length > 0)
                    {
                        tokens.Add(new HavingToken(sb.ToString(), pos - sb.Length));
                        sb.Clear();
                    }
                    tokens.Add(new HavingToken(ch.ToString(), pos));
                    pos++;
                    continue;
                }
                case '\'' or '"':
                {
                    if (sb.Length > 0)
                    {
                        tokens.Add(new HavingToken(sb.ToString(), pos - sb.Length));
                        sb.Clear();
                    }

                    var quote = ch;
                    pos++;
                    var valueStart = pos;
                    while (pos < input.Length && input[pos] != quote)
                        pos++;

                    var value = input[valueStart..pos];
                    if (pos < input.Length) pos++; // skip closing quote

                    tokens.Add(new HavingToken(value, valueStart));
                    continue;
                }
                default:
                    sb.Append(ch);
                    pos++;
                    break;
            }
        }

        if (sb.Length > 0)
        {
            tokens.Add(new HavingToken(sb.ToString(), pos - sb.Length));
        }

        return tokens;
    }

    private sealed class HavingTokenParser(List<HavingToken> tokens)
    {
        private int _position;
        public HavingToken? CurrentToken => _position < tokens.Count ? tokens[_position] : null;

        private HavingToken Expect(string expected)
        {
            if (_position >= tokens.Count)
                throw new DslParseException($"Expected '{expected}' but reached end of HAVING expression.");

            var token = tokens[_position];
            if (!string.Equals(token.Value, expected, StringComparison.OrdinalIgnoreCase))
                throw new DslParseException($"Expected '{expected}' but found '{token.Value}' at position {token.Position}.");

            _position++;
            return token;
        }

        private bool Match(string value)
        {
            if (_position >= tokens.Count ||
                !string.Equals(tokens[_position].Value, value, StringComparison.OrdinalIgnoreCase)) return false;
            _position++;
            return true;
        }

        private HavingToken Consume()
        {
            return _position >= tokens.Count ? throw new DslParseException($"Unexpected end of HAVING expression.") : tokens[_position++];
        }

        public HavingNode ParseExpression()
        {
            return ParseOr();
        }

        private HavingNode ParseOr()
        {
            var left = ParseAnd();

            while (Match("OR"))
            {
                var right = ParseAnd();
                left = new HavingLogicalNode { Logic = LogicOperator.Or, Children = [left, right] };
            }

            return left;
        }

        private HavingNode ParseAnd()
        {
            var left = ParseFactor();

            while (Match("AND"))
            {
                var right = ParseFactor();
                left = new HavingLogicalNode { Logic = LogicOperator.And, Children = [left, right] };
            }

            return left;
        }

        private HavingNode ParseFactor()
        {
            if (!Match("(")) return ParseCondition();
            var inner = ParseExpression();
            Expect(")");
            return new HavingGroupNode { Inner = inner };

        }

        private HavingNode ParseCondition()
        {
            if (_position >= tokens.Count)
                throw new DslParseException("Expected HAVING condition but reached end of expression.");

            var conditionParts = new List<string>();
            var startPos = tokens[_position].Position;

            while (_position < tokens.Count && tokens[_position].Value is not ("AND" or "OR" or "(" or ")"))
            {
                conditionParts.Add(tokens[_position].Value);
                _position++;
            }

            if (conditionParts.Count == 0)
                throw new DslParseException("Expected HAVING condition but found logical operator.");

            var conditionToken = string.Join(':', conditionParts);
            var parts = conditionToken.Split(':', StringSplitOptions.TrimEntries);

            if (parts.Length < 4)
                throw new DslParseException(
                    $"Unable to parse HAVING condition '{conditionToken}'. " +
                    "Expected format: FUNCTION:Field:Operator:value. " +
                    "For example: sum:total:gt:100");

            var fnRaw = parts[0].ToLowerInvariant();
            if (fnRaw == "average") fnRaw = "avg";

            if (string.IsNullOrWhiteSpace(fnRaw))
                throw new DslParseException(
                    $"Unable to parse HAVING condition '{conditionToken}'. " +
                    "Expected format: FUNCTION:Field:Operator:value. " +
                    "For example: sum:total:gt:100");

            if (!Enum.TryParse<AggregateFunction>(fnRaw, true, out var function))
                throw new DslParseException(
                    $"Unsupported aggregate function '{parts[0]}' in HAVING condition '{conditionToken}'. " +
                    "Supported functions: sum, count, avg, min, max.");

            var field = parts[1];
            if (string.IsNullOrWhiteSpace(field))
                throw new DslParseException(
                    $"Missing field in HAVING condition '{conditionToken}'. " +
                    "Expected format: FUNCTION:Field:Operator:value.");

            if (field == "*")
                throw new DslParseException(
                    $"Invalid field '{field}' in HAVING condition '{conditionToken}'. " +
                    "COUNT(*) is not supported. Use COUNT(<field>) instead.");

            if (!IsValidPropertyPath(field.AsSpan()))
                throw new DslParseException(
                    $"Invalid field '{field}' in HAVING condition '{conditionToken}'. " +
                    "Field must be a valid property path.");

            var opRaw = parts[2];
            var normalizedOp = FilterOperators.Normalize(opRaw);
            var allowedOps = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "eq", "neq", "gt", "gte", "lt", "lte" };
            if (!allowedOps.Contains(normalizedOp))
                throw new DslParseException(
                    $"Unsupported operator '{opRaw}' in HAVING condition '{conditionToken}'. " +
                    "Supported operators: eq, neq, gt, gte, lt, lte.");

            var value = string.Join(':', parts[3..]);

            if (string.IsNullOrWhiteSpace(value))
                throw new DslParseException(
                    $"Missing value after operator in HAVING condition '{conditionToken}'.");

            return new HavingConditionNode
            {
                Function = function,
                Field = string.IsNullOrWhiteSpace(field) ? null : field,
                Operator = normalizedOp,
                Value = value
            };
        }

        private static bool IsValidPropertyPath(ReadOnlySpan<char> path)
        {
            if (path.IsEmpty) return true;
            if (path[0] == '.') return false;
            if (path[^1] == '.') return false;

            var segmentStart = 0;
            for (var i = 0; i <= path.Length; i++)
            {
                if (i != path.Length && path[i] != '.') continue;
                if (i == segmentStart) return false;
                if (!char.IsLetter(path[segmentStart]) && path[segmentStart] != '_') return false;
                for (var j = segmentStart + 1; j < i; j++)
                {
                    if (!char.IsLetterOrDigit(path[j]) && path[j] != '_') return false;
                }
                segmentStart = i + 1;
            }
            return true;
        }
    }
}
