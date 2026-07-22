using System.Text.RegularExpressions;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Mapping.Metadata;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Dapper.Sql.Builders;

internal static class SqlSimpleIncludeQueryBuilder
{
    private const string RootAlias = "__fq_root";

    public static bool CanBuild(QueryOptions options, IEntityMapping rootMapping, IMappingRegistry registry)
    {
        if (options.Includes is not { Count: 1 }) return false;
        if (options.Expand is { Count: > 0 }) return false;
        if (options.ProjectionMode != ProjectionMode.Nested) return false;
        if (options.GroupBy is { Count: > 0 } || options.Aggregates.Count > 0) return false;
        if (options.Distinct == true || options.IsKeysetMode) return false;

        var include = options.Includes[0];
        if (include.Contains('.', StringComparison.Ordinal)) return false;

        var rel = rootMapping.GetRelationship(include);
        if (rel?.TargetType is null || rel.RelationshipType != RelationshipType.OneToMany) return false;

        _ = registry.GetMapping(rel.TargetType);
        return true;
    }

    public static SimpleIncludeSqlCommand Build(
        QueryOptions options,
        IEntityMapping rootMapping,
        IMappingRegistry registry,
        ISqlDialect dialect,
        SqlTranslator translator)
    {
        var include = options.Includes![0];
        var rel = rootMapping.GetRelationship(include)!;
        var childMapping = registry.GetMapping(rel.TargetType);

        var rootOptions = options.CopyQueryOptions();
        rootOptions.Includes = null;
        rootOptions.Expand = null;
        rootOptions.Select = null;
        rootOptions.SelectTree = null;

        var rootCommand = translator.Translate(rootOptions);
        var rootSelect = BuildRootSelect(rootMapping, dialect);
        var childSelect = BuildChildSelect(include, rel.NavigationPropertyName, childMapping, dialect);
        var childAlias = rel.NavigationPropertyName;
        var joinCondition = SqlSyntaxBuilder.BuildJoinCondition(
            dialect,
            rel,
            rootMapping,
            RootAlias,
            childMapping,
            childAlias);

        var sqlParts = new List<string>
        {
            $"SELECT {rootSelect}, {childSelect}",
            $"FROM ({rootCommand.Sql}) AS {dialect.QuoteIdentifier(RootAlias)}",
            $"LEFT JOIN {SqlSyntaxBuilder.QuoteTable(dialect, childMapping)} AS {dialect.QuoteIdentifier(childAlias)} ON {joinCondition}"
        };

        var outerOrderBy = BuildOuterOrderBy(options.Sort, rootMapping, dialect);
        if (!string.IsNullOrEmpty(outerOrderBy))
            sqlParts.Add(outerOrderBy);

        var sql = Regex.Replace(string.Join(" ", sqlParts), @"\s+", " ");

        return new SimpleIncludeSqlCommand
        {
            Sql = sql,
            Parameters = rootCommand.Parameters,
            IncludePath = include,
            ChildMapping = childMapping
        };
    }

    private static string BuildRootSelect(IEntityMapping mapping, ISqlDialect dialect)
    {
        return string.Join(", ", mapping.GetProperties().Select(prop =>
        {
            var column = mapping.GetColumnName(prop);
            return $"{dialect.QuoteIdentifier(RootAlias)}.{dialect.QuoteIdentifier(column)} AS {dialect.QuoteIdentifier(column)}";
        }));
    }

    private static string BuildChildSelect(string include, string childAlias, IEntityMapping mapping, ISqlDialect dialect)
    {
        return string.Join(", ", mapping.GetProperties().Select(prop =>
        {
            var column = mapping.GetColumnName(prop);
            var output = include + "_" + column;
            return $"{dialect.QuoteIdentifier(childAlias)}.{dialect.QuoteIdentifier(column)} AS {dialect.QuoteIdentifier(output)}";
        }));
    }

    private static string BuildOuterOrderBy(IReadOnlyList<SortNode>? sorts, IEntityMapping mapping, ISqlDialect dialect)
    {
        if (sorts is not { Count: > 0 }) return string.Empty;

        var parts = sorts.Select(sort =>
        {
            var column = mapping.GetProperties().Contains(sort.Field, StringComparer.OrdinalIgnoreCase)
                ? mapping.GetColumnName(sort.Field)
                : sort.Field;

            var expr = $"{dialect.QuoteIdentifier(RootAlias)}.{dialect.QuoteIdentifier(column)}";
            return sort.Descending ? $"{expr} DESC" : expr;
        });

        return $"ORDER BY {string.Join(", ", parts)}";
    }
}

internal sealed class SimpleIncludeSqlCommand
{
    public string Sql { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();
    public string IncludePath { get; init; } = string.Empty;
    public IEntityMapping ChildMapping { get; init; } = null!;
}
