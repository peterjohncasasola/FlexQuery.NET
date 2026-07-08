using FlexQuery.NET.EntityFrameworkCore.SqlFormatting;
using FlexQuery.NET.Models;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.EntityFrameworkCore.Execution;

/// <summary>
/// Best-effort extraction of the generated SQL and its parameters from an
/// <see cref="IQueryable"/>, for diagnostic/listener events. Never throws —
/// translation failures degrade to a null SQL string rather than surfacing
/// to the caller.
/// </summary>
internal static class SqlQueryInspector
{
    public static (string? Sql, IReadOnlyList<QueryParameter>? Parameters) TryGetSqlWithParameters(IQueryable query)
    {
        try
        {
            var rawSql = query.ToQueryString();
            var (cleanSql, parameters) = SqlParameterExtractor.Extract(rawSql);
            try { return (SqlFormatter.Format(cleanSql), parameters); }
            catch { return (cleanSql, parameters); }
        }
        catch { return (null, null); }
    }
}