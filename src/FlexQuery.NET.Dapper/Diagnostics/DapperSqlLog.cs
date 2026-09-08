using System.Globalization;
using System.Text;
using FlexQuery.NET.SqlFormatting;
using Microsoft.Extensions.Logging;

namespace FlexQuery.NET.Dapper.Diagnostics;

/// <summary>
/// Emits one Information-level log entry per Dapper command, immediately before the
/// command executes, as a readable, copy-paste-ready SQL script: the final SQL
/// (formatted with the project's existing <see cref="SqlFormatter"/>) preceded by a
/// DECLARE block that embeds the parameter values as SQL literals. The Dapper
/// counterpart of the EF Core <c>Microsoft.EntityFrameworkCore.Database.Command</c> logs.
/// </summary>
/// <remarks>
/// Logging is strictly non-invasive: the helper only reads the SQL and the parameter
/// dictionary that already feed the Dapper call — it never mutates them, never creates
/// parameter objects, and never executes anything. The DECLARE block and the SQL type
/// names are diagnostics-only; Dapper keeps receiving the exact same SQL string and
/// parameter object. A <see langword="null"/> logger (or a logger with
/// <see cref="LogLevel.Information"/> disabled) short-circuits to a no-op, so disabled
/// logging costs a single null check.
/// </remarks>
internal static class DapperSqlLog
{
    /// <summary>The logger category used for all Dapper SQL execution logs.</summary>
    internal const string CategoryName = "FlexQuery.NET.Dapper";

    /// <summary>
    /// Logs the final SQL and parameters of a Dapper command immediately before execution.
    /// </summary>
    /// <param name="logger">The request-scoped logger, or <see langword="null"/> when logging is disabled.</param>
    /// <param name="sql">The exact final SQL string passed to Dapper.</param>
    /// <param name="parameters">The parameter name/value pairs actually passed to Dapper.</param>
    internal static void Command(ILogger? logger, string sql, IEnumerable<KeyValuePair<string, object?>> parameters)
    {
        if (logger is null || !logger.IsEnabled(LogLevel.Information))
            return;

        try
        {
            logger.LogInformation(BuildMessage(sql, parameters));
        }
        catch
        {
            // Diagnostics must never break query execution.
        }
    }

    private static string BuildMessage(string sql, IEnumerable<KeyValuePair<string, object?>> parameters)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Executing Dapper query");
        sb.AppendLine();
        sb.AppendLine("SQL:");

        var declarations = BuildDeclarations(parameters);
        if (declarations.Count > 0)
        {
            sb.AppendLine("DECLARE");
            sb.Append(string.Join(",\n", declarations.Select(d => "    " + d)));
            sb.AppendLine(";");
            sb.AppendLine();
        }

        sb.Append(SqlFormatter.Format(sql).TrimEnd());
        return sb.ToString();
    }

    private static List<string> BuildDeclarations(IEnumerable<KeyValuePair<string, object?>> parameters)
    {
        var declarations = new List<string>();
        foreach (var parameter in parameters)
        {
            var name = NormalizeName(parameter.Key);
            declarations.Add($"{name} AS {GetSqlType(parameter.Value)} = {FormatLiteral(parameter.Value)}");
        }

        return declarations;
    }

    /// <summary>Normalizes dialect parameter prefixes to the T-SQL '@' form.</summary>
    private static string NormalizeName(string name)
        => name.StartsWith('@') ? name : "@" + name.TrimStart(':', '?');

    /// <summary>
    /// Infers a T-SQL type for the DECLARE script from the runtime value. These names
    /// are diagnostics-only; no Dapper parameter metadata is created or modified.
    /// </summary>
    private static string GetSqlType(object? value) => value switch
    {
        null or DBNull => "NVARCHAR(1)",
        byte => "TINYINT",
        sbyte => "SMALLINT",
        short => "SMALLINT",
        ushort => "INT",
        int => "INT",
        uint => "BIGINT",
        long => "BIGINT",
        ulong => "DECIMAL(20, 0)",
        bool => "BIT",
        decimal => "DECIMAL(38, 18)",
        float => "REAL",
        double => "FLOAT",
        Guid => "UNIQUEIDENTIFIER",
        DateTime => "DATETIME2",
        DateTimeOffset => "DATETIMEOFFSET",
        DateOnly => "DATE",
        TimeOnly or TimeSpan => "TIME",
        byte[] => "VARBINARY(MAX)",
        char => "NVARCHAR(1)",
        string s => $"NVARCHAR({Math.Max(1, s.Length)})",
        Enum => "INT",
        _ => "NVARCHAR(MAX)"
    };

    /// <summary>Renders the value as a SQL literal for the DECLARE script.</summary>
    private static string FormatLiteral(object? value) => value switch
    {
        null or DBNull => "NULL",
        string s => QuoteSqlString(s),
        char c => QuoteSqlString(c.ToString()),
        Guid g => QuoteSqlString(g.ToString()),
        bool b => b ? "1" : "0",
        DateTime dt => QuoteSqlString(dt.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture)),
        DateTimeOffset dto => QuoteSqlString(dto.ToString("yyyy-MM-dd HH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture)),
        DateOnly d => QuoteSqlString(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        TimeOnly t => QuoteSqlString(t.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture)),
        TimeSpan ts => QuoteSqlString(ts.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture)),
        byte[] bytes => FormatBinary(bytes),
        Enum e => FormatEnum(e),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => QuoteSqlString(value.ToString() ?? string.Empty)
    };

    private static string QuoteSqlString(string value)
        => "'" + value.Replace("'", "''") + "'";

    private static string FormatEnum(Enum value)
        => Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()), CultureInfo.InvariantCulture)
            ?.ToString() ?? "NULL";

    private static string FormatBinary(byte[] bytes)
        => bytes.Length == 0 ? "0x" : "0x" + Convert.ToHexString(bytes);
}
