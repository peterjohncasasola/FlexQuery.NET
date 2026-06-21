using FlexQuery.NET.Builders;
using FlexQuery.NET.Models;
namespace FlexQuery.NET.Parsers.Jql;

public sealed class JqlQueryParser : IQueryParser
{
    /// <inheritdoc />
    public QuerySyntax Syntax => QuerySyntax.Jql;

    public FilterGroup Parse(string filter)
    {
        var ast = JqlAstParser.Parse(filter);
        return JqlFilterConverter.ToFilterGroup(ast);
    }

    public bool CanParse(FlexQueryParameters parameters)
    {
        var filter = parameters.Filter;
        if (string.IsNullOrWhiteSpace(filter))
            return false;

        if (filter.StartsWith('!'))
            return false;

        var trimmed = filter.TrimStart();
        if (trimmed.Length > 0 && trimmed[0] == '{')
            return false;

        if (filter.Contains('=') || filter.Contains('>') || filter.Contains('<'))
            return true;

        if (filter.Contains(':'))
        {
            if (!HasDefinitiveJqlInScopes(filter))
                return false;
        }

        return true;
    }

    private static bool HasDefinitiveJqlInScopes(string filter)
    {
        foreach (var opener in new[] { ".any(", ".all(" })
        {
            var searchStart = 0;
            while (true)
            {
                var start = filter.IndexOf(opener, searchStart, StringComparison.OrdinalIgnoreCase);
                if (start < 0)
                    break;

                start += opener.Length;
                var depth = 1;
                var end = start;
                while (end < filter.Length && depth > 0)
                {
                    if (filter[end] == '(') depth++;
                    else if (filter[end] == ')') depth--;
                    if (depth > 0) end++;
                }

                var inner = filter.AsSpan(start, end - start);
                if (inner.Contains('=') || inner.Contains('>') || inner.Contains('<'))
                    return true;

                searchStart = end + 1;
            }
        }
        return false;
    }

    public QueryOptions Parse(FlexQueryParameters parameters)
    {
        var options = QueryOptionsFactory.Create(parameters);
        if (!string.IsNullOrWhiteSpace(parameters.Filter))
        {
            var ast = JqlAstParser.Parse(parameters.Filter);
            options.Filter = JqlFilterConverter.ToFilterGroup(ast);
            options.Ast = ast;
            options.Filter = FilterNormalizer.NormalizeOrder(options.Filter);
        }
        return options;
    }
}
