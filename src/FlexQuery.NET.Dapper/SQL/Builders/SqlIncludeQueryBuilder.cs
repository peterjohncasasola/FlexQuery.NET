using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Dapper.Sql.Builders;

internal static class SqlIncludeQueryBuilder
{
    public static string BuildIncludeSql(
        string navigationPath,
        IEntityMapping rootMapping,
        IEntityMapping targetMapping,
        ISqlDialect dialect,
        IncludeNode? expandNode,
        IReadOnlyList<object> rootPkValues,
        SqlParameterContext parameters)
    {
        if (rootPkValues.Count == 0) return string.Empty;

        var navAlias = navigationPath;
        var rel = rootMapping.GetRelationship(navigationPath);
        var fkColumn = !string.IsNullOrEmpty(rel?.ForeignKey)
            ? targetMapping.GetColumnName(rel.ForeignKey)
            : (targetMapping.GetKeyProperties().FirstOrDefault()?.ToString() ?? "Id");
        var where = BuildInClause(dialect, navAlias, fkColumn, rootPkValues, parameters);
        var filterWhere = expandNode?.Filter != null
            ? BuildFilterWhere(expandNode.Filter, targetMapping, parameters, dialect)
            : string.Empty;

        var whereClause = CombineWhere(where, filterWhere);
        if (expandNode?.Take is > 0)
        {
            return BuildPartitionedTakeSql(
                navigationPath,
                targetMapping,
                dialect,
                whereClause,
                fkColumn,
                expandNode,
                parameters);
        }

        var orderBy = BuildOrderBy(expandNode?.Sort, targetMapping, dialect, qualifyWithAlias: false);

        var allProps = targetMapping.GetProperties().ToList();
        var selectParts = new List<string>();
        foreach (var prop in allProps)
        {
            var col = targetMapping.GetColumnName(prop);
            var outputName = navAlias + "_" + col;
            selectParts.Add($"{dialect.QuoteIdentifier(navAlias)}.{dialect.QuoteIdentifier(col)} AS {dialect.QuoteIdentifier(outputName)}");
        }

        var selectClause = $"SELECT {string.Join(", ", selectParts)}";
        var fromClause = $"FROM {SqlSyntaxBuilder.QuoteTable(dialect, targetMapping)} AS {dialect.QuoteIdentifier(navAlias)}";

        var sql = new List<string> { selectClause, fromClause };
        if (!string.IsNullOrEmpty(whereClause)) sql.Add($"WHERE {whereClause}");
        if (!string.IsNullOrEmpty(orderBy)) sql.Add(orderBy);

        return string.Join(" ", sql);
    }

    private static string BuildPartitionedTakeSql(
        string navigationPath,
        IEntityMapping targetMapping,
        ISqlDialect dialect,
        string whereClause,
        string fkColumn,
        IncludeNode expandNode,
        SqlParameterContext parameters)
    {
        var navAlias = navigationPath;
        var rankedAlias = navigationPath + "_ranked";
        const string rowNumberAlias = "__fq_row_number";
        var takeParam = parameters.Add(expandNode.Take!.Value);
        var allProps = targetMapping.GetProperties().ToList();

        var innerSelectParts = new List<string>();
        var outerSelectParts = new List<string>();
        foreach (var prop in allProps)
        {
            var col = targetMapping.GetColumnName(prop);
            var outputName = navAlias + "_" + col;
            innerSelectParts.Add($"{dialect.QuoteIdentifier(navAlias)}.{dialect.QuoteIdentifier(col)} AS {dialect.QuoteIdentifier(outputName)}");
            outerSelectParts.Add($"{dialect.QuoteIdentifier(rankedAlias)}.{dialect.QuoteIdentifier(outputName)} AS {dialect.QuoteIdentifier(outputName)}");
        }

        var rowNumberOrderBy = BuildWindowOrderBy(expandNode.Sort, targetMapping, dialect, navAlias);
        innerSelectParts.Add(
            $"ROW_NUMBER() OVER (PARTITION BY {dialect.QuoteIdentifier(navAlias)}.{dialect.QuoteIdentifier(fkColumn)} ORDER BY {rowNumberOrderBy}) AS {dialect.QuoteIdentifier(rowNumberAlias)}");

        var sql = new List<string>
        {
            $"SELECT {string.Join(", ", outerSelectParts)}",
            "FROM (",
            $"SELECT {string.Join(", ", innerSelectParts)}",
            $"FROM {SqlSyntaxBuilder.QuoteTable(dialect, targetMapping)} AS {dialect.QuoteIdentifier(navAlias)}"
        };

        if (!string.IsNullOrEmpty(whereClause)) sql.Add($"WHERE {whereClause}");

        sql.Add($") AS {dialect.QuoteIdentifier(rankedAlias)}");
        sql.Add($"WHERE {dialect.QuoteIdentifier(rankedAlias)}.{dialect.QuoteIdentifier(rowNumberAlias)} <= {takeParam}");
        sql.Add($"ORDER BY {dialect.QuoteIdentifier(rankedAlias)}.{dialect.QuoteIdentifier(navAlias + "_" + fkColumn)}, {dialect.QuoteIdentifier(rankedAlias)}.{dialect.QuoteIdentifier(rowNumberAlias)}");

        return string.Join(" ", sql);
    }

    private static string BuildFilterWhere(
        FilterGroup filter,
        IEntityMapping mapping,
        SqlParameterContext parameters,
        ISqlDialect dialect)
    {
        var whereBuilder = new SqlWhereBuilder(null, dialect, null!, null!);
        return whereBuilder.BuildFilterGroupExpression(filter, mapping, parameters);
    }

    private static string CombineWhere(string where1, string where2)
    {
        if (string.IsNullOrEmpty(where1)) return where2;
        return string.IsNullOrEmpty(where2) ? where1 : $"({where1}) AND ({where2})";
    }

    private static string BuildOrderBy(List<SortNode>? sortNodes, IEntityMapping mapping, ISqlDialect dialect, bool qualifyWithAlias, string? alias = null)
    {
        if (sortNodes == null || sortNodes.Count == 0) return string.Empty;

        var parts = new List<string>();
        foreach (var sort in sortNodes)
        {
            var col = mapping.GetColumnName(sort.Field);
            var quotedCol = qualifyWithAlias && !string.IsNullOrEmpty(alias)
                ? $"{dialect.QuoteIdentifier(alias)}.{dialect.QuoteIdentifier(col)}"
                : dialect.QuoteIdentifier(col);
            parts.Add(sort.Descending ? $"{quotedCol} DESC" : quotedCol);
        }

        return $"ORDER BY {string.Join(", ", parts)}";
    }

    private static string BuildWindowOrderBy(List<SortNode>? sortNodes, IEntityMapping mapping, ISqlDialect dialect, string alias)
    {
        var orderBy = BuildOrderBy(sortNodes, mapping, dialect, qualifyWithAlias: true, alias);
        if (!string.IsNullOrEmpty(orderBy))
            return orderBy["ORDER BY ".Length..];

        var keyProperty = mapping.GetKeyProperties().FirstOrDefault()
            ?? mapping.GetProperties().FirstOrDefault(p => p.Equals("Id", StringComparison.OrdinalIgnoreCase))
            ?? mapping.GetProperties().First();
        var keyColumn = mapping.GetColumnName(keyProperty);
        return $"{dialect.QuoteIdentifier(alias)}.{dialect.QuoteIdentifier(keyColumn)}";
    }

    private static string BuildInClause(
        ISqlDialect dialect,
        string navAlias,
        string fkColumn,
        IReadOnlyList<object> rootPkValues,
        SqlParameterContext parameters)
    {
        var paramNames = new List<string>();
        foreach (var pk in rootPkValues)
        {
            var paramName = parameters.Add(pk!);
            paramNames.Add(paramName);
        }

        return $"{dialect.QuoteIdentifier(navAlias)}.{dialect.QuoteIdentifier(fkColumn)} IN ({string.Join(", ", paramNames)})";
    }
}
