using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Filters;

namespace FlexQuery.NET.Parsers.Fql;

/// <summary>
/// Parses FQL expand expressions into <see cref="ExpandAst"/> trees.
/// <para>
/// Grammar: <c>expand = path(option; option; ...), path(...)</c>
/// Options: <c>filter=...</c>, <c>sort=...</c>, <c>take=N</c>
/// </para>
/// </summary>
internal static class FqlExpandParser
{
    /// <summary>Parses an FQL expand string into a list of ExpandAst roots.</summary>
    public static List<ExpandAst> Parse(string? expandRaw)
    {
        if (string.IsNullOrWhiteSpace(expandRaw))
            return [];

        var tokens = new FqlTokenizer(expandRaw).Tokenize();
        var parser = new Parser(tokens, expandRaw);
        return parser.ParseExpandList();
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

        public List<ExpandAst> ParseExpandList()
        {
            var result = new List<ExpandAst>();
            result.Add(ParseExpandBlock());

            while (Match(FqlTokenType.Comma))
            {
                result.Add(ParseExpandBlock());
            }

            while (Match(FqlTokenType.CloseParen))
            {
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

        private ExpandAst ParseExpandBlock()
        {
            var path = new List<string>();
            path.Add(Expect(FqlTokenType.Identifier).Value);

            while (Match(FqlTokenType.Dot))
            {
                if (Current.Kind == FqlTokenType.Identifier)
                {
                    path.Add(_tokens[_position++].Value);
                }
                else
                {
                    break;
                }
            }

            var ast = new ExpandAst
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

        private void ParseOptionsAndChildren(ExpandAst parent)
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
                    else if (key.Equals("take", StringComparison.OrdinalIgnoreCase))
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
                }
                else if (nextIsOpenParen)
                {
                    _position++;
                    var childPath = new List<string> { key };

                    while (Match(FqlTokenType.Dot))
                    {
                        if (Current.Kind == FqlTokenType.Identifier)
                        {
                            childPath.Add(_tokens[_position++].Value);
                        }
                        else
                        {
                            break;
                        }
                    }

                    Expect(FqlTokenType.OpenParen);
                    var child = new ExpandAst
                    {
                        Path = childPath,
                        Children = []
                    };
                    ParseOptionsAndChildren(child);
                    Expect(FqlTokenType.CloseParen);
                    parent.Children.Add(child);
                }
                else
                {
                    throw new FqlParseException(
                        $"Unexpected expand option '{key}'. Expected filter, sort, or take.",
                        position: keyToken.Position);
                }

                if (Current.Kind == FqlTokenType.Semicolon || Current.Kind == FqlTokenType.Comma)
                {
                    _position++;
                    continue;
                }

                break;
            }
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