using FlexQuery.NET.Builders;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.EntityFrameworkCore;

/// <summary>
/// EF Core extensions for SQL preview and projection explain functionality.
/// </summary>
public static class ProjectionEfCoreExtensions
{
    /// <summary>
    /// Gets a preview of the generated SQL without executing the query.
    /// Works after dynamic projections are applied.
    /// </summary>
    /// <param name="query">The projected queryable.</param>
    /// <returns>The generated SQL string.</returns>
    public static string ToSqlPreview(this IQueryable query)
    {
        // Try to get ToQueryString via reflection for EF Core queries
        var toQueryStringMethod = query.GetType().GetMethod("ToQueryString");
        if (toQueryStringMethod != null)
        {
            return toQueryStringMethod.Invoke(query, null) as string ?? string.Empty;
        }

        // For non-EF Core queries, return a placeholder
        return "<Not available: query is not EF Core translatable>";
    }

    /// <summary>
    /// Gets a SQL preview after applying FlexQuery options.
    /// </summary>
    /// <param name="query">The source queryable.</param>
    /// <param name="parameters">The FlexQuery parameters.</param>
    /// <param name="configure">Optional query execution configuration.</param>
    /// <returns>The generated SQL string.</returns>
    public static string ToSqlPreview<T>(
        this IQueryable<T> query,
        FlexQueryParameters parameters,
        Action<QueryExecutionOptions>? configure = null)
        where T : class
    {
        var options = parameters.ToQueryOptions();
        var projected = query.ApplySelect(options);
        return projected.ToSqlPreview();
    }

    /// <summary>
    /// Explains the projection plan including selected fields, navigation usage,
    /// and optimization notes.
    /// </summary>
    /// <param name="query">The projected queryable.</param>
    /// <param name="options">The query options.</param>
    /// <returns>A human-readable explanation of the projection.</returns>
    public static ProjectionExplanation ExplainProjection(this IQueryable query, QueryOptions options)
    {
        var tree = SelectTreeBuilder.Build(options);
        
        var explanation = new ProjectionExplanation
        {
            SelectedFields = ExtractSelectedFields(options),
            EstimatedColumns = CountEstimatedColumns(options, tree),
            NavigationUsage = ExtractNavigationUsage(options, tree),
            OptimizationNotes = AnalyzeOptimizations(options, tree)
        };

        return explanation;
    }

    /// <summary>
    /// Explains the projection plan for a generic queryable.
    /// </summary>
    public static ProjectionExplanation ExplainProjection<T>(this IQueryable<T> query, QueryOptions options)
        where T : class
    {
        var projected = query.ApplySelect(options);
        return projected.ExplainProjection(options);
    }

    private static IReadOnlyList<string> ExtractSelectedFields(QueryOptions options)
    {
        var fields = new List<string>();

        if (options.Select != null)
        {
            fields.AddRange(options.Select.Select(s => s.Field));
        }

        if (options.Includes != null)
        {
            fields.AddRange(options.Includes.Select(i => i.Split(' ').First()));
        }

        return fields.Distinct().ToList();
    }

    private static int CountEstimatedColumns(QueryOptions options, SelectionNode tree)
    {
        var count = 0;

        if (options.Select != null)
        {
            count += options.Select.Count;
        }

        // Expand includes to count their scalar properties
        if (options.Includes != null)
        {
            // Simplified: each include typically brings 3-5 columns
            count += options.Includes.Count * 3;
        }

        return Math.Max(count, tree.Count);
    }

    private static IReadOnlyDictionary<string, string> ExtractNavigationUsage(QueryOptions options, SelectionNode tree)
    {
        var usages = new Dictionary<string, string>();

        if (options.Includes != null)
        {
            foreach (var include in options.Includes)
            {
                var path = include.Split(' ').First();
                usages[path] = $"Include: {path}";
            }
        }

        // Add navigation paths from tree
        foreach (var child in tree.EnumerateChildren())
        {
            usages[child.Key] = child.Key;
        }

        return usages;
    }

    private static IReadOnlyList<string> AnalyzeOptimizations(QueryOptions options, SelectionNode tree)
    {
        var notes = new List<string>();

        // Check for projection optimization opportunities
        if (options.Select != null && options.Includes != null)
        {
            notes.Add("Consider: Includes may be redundant when Select explicitly specifies fields");
        }

        // Check for potential over-fetching
        if (tree.IncludeAllScalars)
        {
            notes.Add("IncludeAllScalars enabled: Full entity materialization may occur");
        }

        // Check for collection navigation in projection
        if (tree.EnumerateChildren().Any(c => IsCollectionNavigation(c.Key, options)))
        {
            notes.Add("Projection involves collection navigation: Flattened output will use SelectMany");
        }

        return notes;
    }

    private static bool IsCollectionNavigation(string propertyName, QueryOptions options)
    {
        // Simplified check - in real implementation would check the actual entity type
        var navProps = new[] { "orders", "orderitems", "items" };
        return navProps.Contains(propertyName, StringComparer.OrdinalIgnoreCase);
    }
}