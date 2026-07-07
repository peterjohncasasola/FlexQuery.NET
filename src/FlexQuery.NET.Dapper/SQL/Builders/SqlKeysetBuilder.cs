using FlexQuery.NET.Caching;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Validation;

namespace FlexQuery.NET.Dapper.Sql.Builders;

internal static class SqlKeysetBuilder
{
    public static SqlKeysetResult BuildSeekClause(
        IReadOnlyList<SortNode> sorts,
        KeysetCursor cursor,
        IEntityMapping mapping,
        ISqlDialect dialect,
        SqlParameterContext parameters)
    {
        if (sorts.Count == 0)
            throw new InvalidOperationException("Keyset pagination requires at least one sort field.");

        var keyTypes = new Type[sorts.Count];
        var columns = new string[sorts.Count];
        var descending = new bool[sorts.Count];
        var paramNames = new string[sorts.Count];

        for (var i = 0; i < sorts.Count; i++)
        {
            var sort = sorts[i];

            keyTypes[i] = ReflectionCache.GetProperty(mapping.Type, sort.Field)?.PropertyType ?? typeof(object);
            columns[i] = SqlSyntaxBuilder.QuoteColumn(dialect, mapping.GetColumnName(sort.Field), mapping);
            descending[i] = sort.Descending;
        }

        KeysetCursorValidator.Validate(cursor.Values, keyTypes);

        for (var i = 0; i < sorts.Count; i++)
        {
            paramNames[i] = parameters.Add(cursor.Values[i]);
        }

        var orderByColumns = new string[sorts.Count];
        for (var i = 0; i < sorts.Count; i++)
        {
            orderByColumns[i] = descending[i] ? $"{columns[i]} DESC" : columns[i];
        }

        var whereClause = BuildWhereClause(columns, descending, paramNames, cursor.Values);
        var orderByClause = $"ORDER BY {string.Join(", ", orderByColumns)}";

        return new SqlKeysetResult
        {
            WhereClause = whereClause,
            OrderByClause = orderByClause
        };
    }

    private static string BuildWhereClause(
        string[] columns,
        bool[] descending,
        string[] paramNames,
        IReadOnlyList<object?> cursorValues)
    {
        var terms = new List<string>(columns.Length);

        for (var i = 0; i < columns.Length; i++)
        {
            var parts = new List<string>(i + 1);

            for (var j = 0; j < i; j++)
            {
                parts.Add(BuildEquality(columns[j], cursorValues[j], paramNames[j]));
            }

            parts.Add(BuildComparison(columns[i], descending[i], cursorValues[i], paramNames[i]));
            terms.Add($"({string.Join(" AND ", parts)})");
        }

        return string.Join(" OR ", terms);
    }

    private static string BuildComparison(string column, bool descending, object? cursorValue, string paramName)
    {
        if (cursorValue is null)
        {
            return descending ? $"{column} IS NULL" : $"{column} IS NOT NULL";
        }

        var op = descending ? "<" : ">";
        return $"{column} {op} {paramName}";
    }

    private static string BuildEquality(string column, object? cursorValue, string paramName)
    {
        if (cursorValue is null)
            return $"{column} IS NULL";

        return $"{column} = {paramName}";
    }
}
