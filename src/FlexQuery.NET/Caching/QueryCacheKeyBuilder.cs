using System.Linq.Expressions;
using System.Text;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;

namespace FlexQuery.NET.Caching;

internal static class QueryCacheKeyBuilder
{
    public static bool CanCache(QueryOptions options) => true;

    public static string Build(QueryOptions options, Type entityType, string operation)
    {
        var sb = new StringBuilder();
        sb.Append(operation).Append('|')
            .Append(entityType.FullName).Append('|')
            .Append(options.CaseInsensitive ? "ci" : "cs").Append('|')
            .Append("filter=").Append(FilterNormalizer.GenerateCacheKey(options.Filter)).Append('|')
            .Append("sort=").Append(SortKey(options.Sort)).Append('|')
            .Append("select=").Append(ListKey(options.Select)).Append('|')
            .Append("tree=").Append(SelectionKey(options.SelectTree)).Append('|')
            .Append("includes=").Append(ListKey(options.Includes)).Append('|')
            .Append("filteredIncludes=").Append(IncludeKey(options.Expand)).Append('|')
            .Append("mode=").Append(options.ProjectionMode).Append('|')
            .Append("groupBy=").Append(ListKey(options.GroupBy)).Append('|')
            .Append("aggregates=").Append(AggregateKey(options.Aggregates)).Append('|')
            .Append("having=").Append(HavingKey(options.Having)).Append('|')
            .Append("distinct=").Append(options.Distinct).Append('|')
            .Append("efCoreOperators=").Append(options.UseEfCoreOperators).Append('|')
            .Append("exprMappings=").Append(ExpressionMappingsKey(options));

        return sb.ToString();
    }

    private static string ExpressionMappingsKey(QueryOptions options)
    {
        if (!TryGetExpressionMappings(options, out var mappings))
            return string.Empty;

        var entries = mappings
            .OrderBy(m => m.Key, StringComparer.Ordinal)
            .Select(m => $"{Escape(m.Key)}:{Escape(m.Value.ToString())}");
        return string.Join(",", entries);
    }

    private static bool TryGetExpressionMappings(
        QueryOptions options,
        out IReadOnlyDictionary<string, LambdaExpression> mappings)
    {
        if (options.Items.TryGetValue(ContextKeys.ExpressionMappings, out var value)
            && value is IReadOnlyDictionary<string, LambdaExpression> typed)
        {
            mappings = typed;
            return true;
        }

        mappings = new Dictionary<string, LambdaExpression>();
        return false;
    }

    private static string ListKey(IEnumerable<string>? values)
        => values is null
            ? string.Empty
            : string.Join(",", values.Select(Escape).OrderBy(v => v, StringComparer.Ordinal));

    private static string SortKey(IEnumerable<SortNode>? sorts)
        => sorts is null
            ? string.Empty
            : string.Join(",", sorts.Select(s =>
                $"{Escape(s.Field)}:{s.Descending}:{Escape(s.Aggregate)}:{Escape(s.AggregateField)}"));

    private static string AggregateKey(IEnumerable<AggregateModel>? aggregates)
        => aggregates is null
            ? string.Empty
            : string.Join(",", aggregates.Select(a =>
                $"{Escape(a.Function)}:{Escape(a.Field)}:{Escape(a.Alias)}"));

    private static string HavingKey(HavingCondition? having)
        => having is null
            ? string.Empty
            : $"{Escape(having.Function)}:{Escape(having.Field)}:{Escape(having.Operator)}:{Escape(having.Value)}";

    private static string IncludeKey(IEnumerable<IncludeNode>? includes)
        => includes is null
            ? string.Empty
            : string.Join(",", includes.Select(IncludeKey));

    private static string IncludeKey(IncludeNode node)
        => $"{Escape(node.Path)}[{FilterNormalizer.GenerateCacheKey(node.Filter)}]({IncludeKey(node.Children)})";

    private static string SelectionKey(SelectionNode? node)
    {
        if (node is null) return string.Empty;

        var children = node.EnumerateChildren()
            .OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .Select(c => $"{Escape(c.Key)}@{Escape(c.Value.Alias)}:{SelectionKey(c.Value)}");

        return $"{(node.IncludeAllScalars ? "*" : string.Empty)}[{FilterNormalizer.GenerateCacheKey(node.Filter)}]({string.Join(",", children)})";
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace(":", "\\:", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }
}
