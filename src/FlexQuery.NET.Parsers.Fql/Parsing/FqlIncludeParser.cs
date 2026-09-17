using FlexQuery.NET.Filters;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Parsers.Fql;

/// <summary>
/// Parses FQL include expressions into <see cref="IncludeAst"/> trees.
/// <para>
/// Grammar: <c>include = entry *( "," entry )</c>, <c>entry = path [ "(" option *( ";" option ) ")" ]</c>
/// Options: <c>filter=...</c>, <c>sort=...</c>, <c>take=N</c>, and <c>include=...</c> for
/// nested relationships (bare <c>name(...)</c> child blocks are accepted too).
/// </para>
/// </summary>
internal static class FqlIncludeParser
{
    /// <summary>Parses an FQL include string into a list of <see cref="IncludeAst"/> roots.</summary>
    public static List<IncludeAst> Parse(string? includeRaw)
    {
        if (string.IsNullOrWhiteSpace(includeRaw))
            return [];

        var tokens = new FqlTokenizer(includeRaw).Tokenize();
        var parser = new Parser(tokens, includeRaw);
        return parser.ParseIncludeList();
    }

    private sealed class Parser
    {
        private readonly IReadOnlyList<FqlToken> _tokens;
        private readonly string _source;
        private int _position;

        public Parser(IReadOnlyList<FqlToken> tokens, string source)
        {
            _tokens = tokens;
            _source = source;
        }

        private FqlToken Current => _tokens[_position];

        public List<IncludeAst> ParseIncludeList()
        {
            var result = new List<IncludeAst>();
            result.Add(ParseEntry());

            while (Match(FqlTokenType.Comma))
            {
                result.Add(ParseEntry());
            }

            if (Current.Kind != FqlTokenType.End)
            {
                throw new FqlParseException(
                    $"Unexpected token {Current.Kind} ('{Current.Value}'). Expected end of input.",
                    position: Current.Position,
                    found: Current.Value);
            }

            return result;
        }

        private IncludeAst ParseEntry()
        {
            return ParseEntryCore(required: true);
        }

        private IncludeAst ParseEntryCore(bool required)
        {
            var path = new List<string>();

            if (Current.Kind != FqlTokenType.Identifier)
            {
                if (required)
                {
                    throw new FqlParseException(
                        $"Expected a navigation path but found {Current.Kind} ('{Current.Value}').",
                        position: Current.Position,
                        found: Current.Value);
                }

                throw new FqlParseException(
                    "Empty include path. Expected a navigation property name.",
                    position: Current.Position);
            }

            path.Add(ExpectNavigationNameSegment(Expect(FqlTokenType.Identifier)));

            while (Match(FqlTokenType.Dot))
            {
                if (Current.Kind == FqlTokenType.Identifier)
                {
                    path.Add(ExpectNavigationNameSegment(_tokens[_position++]));
                }
                else
                {
                    throw new FqlParseException(
                        "Empty include path segment. Expected a navigation property name after '.'.",
                        position: Current.Position);
                }
            }

            var ast = new IncludeAst
            {
                Path = path,
                Children = []
            };

            if (Current.Kind != FqlTokenType.OpenParen) return ast;
            _position++;
            ParseOptionsAndChildren(ast);
            Expect(FqlTokenType.CloseParen);

            return ast;
        }

        private void ParseOptionsAndChildren(IncludeAst parent)
        {
            while (true)
            {
                if (Current.Kind is FqlTokenType.CloseParen or FqlTokenType.End)
                    break;

                if (Current.Kind != FqlTokenType.Identifier)
                    break;

                var keyToken = Current;
                var key = keyToken.Value;

                var nextIsOpenParen = _position + 1 < _tokens.Count && _tokens[_position + 1].Kind == FqlTokenType.OpenParen;

                if (key.Equals("filter", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("sort", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("take", StringComparison.OrdinalIgnoreCase))
                {
                    _position++;
                    Expect(FqlTokenType.Eq);

                    if (key.Equals("filter", StringComparison.OrdinalIgnoreCase))
                    {
                        var filterRaw = ExtractRawValue();
                        var filterAst = FqlAstParser.Parse(filterRaw);
                        var filterGroup = FqlFilterConverter.ToFilterGroup(filterAst);
                        filterGroup = FilterNormalizer.NormalizeOrder(filterGroup);
                        parent.Filter = filterGroup;
                    }
                    else if (key.Equals("sort", StringComparison.OrdinalIgnoreCase))
                    {
                        var sortRaw = ExtractRawValue();
                        var sortNodes = FqlSortParser.Parse(sortRaw);
                        parent.Sort.AddRange(sortNodes);
                    }
                    else
                    {
                        var numberToken = Expect(FqlTokenType.Number);
                        if (!int.TryParse(numberToken.Value, out var take))
                        {
                            throw new FqlParseException(
                                $"Invalid take value '{numberToken.Value}'. Expected a positive integer.",
                                position: numberToken.Position);
                        }
                        parent.Take = take;
                    }

                    if (Current.Kind == FqlTokenType.Semicolon)
                    {
                        _position++;
                        continue;
                    }
                }
                else if (key.Equals("include", StringComparison.OrdinalIgnoreCase))
                {
                    _position++;
                    Expect(FqlTokenType.Eq);

                    do
                    {
                        parent.Children.Add(ParseEntryCore(required: true));
                    }
                    while (Match(FqlTokenType.Comma) && !PeekIsOptionAssignment());

                    if (Current.Kind == FqlTokenType.Semicolon)
                    {
                        _position++;
                        continue;
                    }
                }
                else if (key.Equals("skip", StringComparison.OrdinalIgnoreCase))
                {
                    throw new FqlParseException(
                        "The 'skip' option is not supported inside include(...). Use 'take=N' to limit the included collection.",
                        position: keyToken.Position);
                }
                else if (nextIsOpenParen)
                {
                    parent.Children.Add(ParseEntryCore(required: false));
                }
                else
                {
                    throw new FqlParseException(
                        $"Unexpected include option '{key}'. Supported options: filter, sort, take, include.",
                        position: keyToken.Position);
                }

                if (Current.Kind == FqlTokenType.Comma)
                {
                    _position++;
                    continue;
                }

                break;
            }
        }

        /// <summary>
        /// True when the upcoming tokens form an option assignment (known option key
        /// followed by '='), used to end nested include entries on comma boundaries.
        /// </summary>
        /// <summary>
        /// Navigation segments must be plain identifiers; the shared FQL tokenizer is
        /// more permissive (e.g. it can fold trailing punctuation into one token), so
        /// the include grammar validates the shape explicitly.
        /// </summary>
        private string ExpectNavigationNameSegment(FqlToken token)
        {
            if (IsPlainIdentifier(token.Value))
                return token.Value;

            throw new FqlParseException(
                $"Invalid navigation name '{token.Value}'. Expected a simple property identifier.",
                position: token.Position,
                found: token.Value);
        }

        private static bool IsPlainIdentifier(string value)
            => value.Length > 0
               && (char.IsLetter(value[0]) || value[0] == '_')
               && value.All(c => char.IsLetterOrDigit(c) || c == '_');

        private bool PeekIsOptionAssignment()
        {
            if (Current.Kind != FqlTokenType.Identifier)
                return false;

            if (_position + 1 >= _tokens.Count || _tokens[_position + 1].Kind != FqlTokenType.Eq)
                return false;

            var value = Current.Value;
            return value.Equals("filter", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("sort", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("take", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("include", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("skip", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Extracts the raw substring from the original source for a filter/sort value.
        /// Scans forward from the current position to find the next top-level semicolon or close-paren,
        /// then extracts the raw text from the original source string.
        /// </summary>
        private string ExtractRawValue()
        {
            if (_position >= _tokens.Count)
                return string.Empty;

            var startToken = _tokens[_position];
            var startChar = startToken.Position;

            var depth = 0;
            while (_position < _tokens.Count)
            {
                var token = _tokens[_position];

                if (token.Kind == FqlTokenType.OpenParen)
                    depth++;

                if (token.Kind == FqlTokenType.CloseParen)
                {
                    if (depth == 0)
                        break;
                    depth--;
                }

                if (token.Kind == FqlTokenType.Semicolon && depth == 0)
                    break;

                if (token.Kind == FqlTokenType.End)
                    break;

                _position++;
            }

            var endToken = _tokens[_position];
            var endChar = endToken.Position;

            return _source[startChar..endChar].Trim();
        }

        private bool Match(FqlTokenType type)
        {
            if (Current.Kind == type)
            {
                _position++;
                return true;
            }
            return false;
        }

        private FqlToken Expect(FqlTokenType type)
        {
            if (Current.Kind != type)
            {
                throw new FqlParseException(
                    $"Expected {type} but found {Current.Kind} ('{Current.Value}').",
                    position: Current.Position,
                    found: Current.Value);
            }
            return _tokens[_position++];
        }
    }
}
