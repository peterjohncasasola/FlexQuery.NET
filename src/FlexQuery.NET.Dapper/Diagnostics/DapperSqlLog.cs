using System.Text;
using Microsoft.Extensions.Logging;

namespace FlexQuery.NET.Dapper.Diagnostics;

/// <summary>
/// Emits one Information-level log entry per Dapper command, immediately before the
/// command executes, containing the exact final SQL string and the parameter name/value
/// pairs actually passed to Dapper. The Dapper counterpart of the EF Core
/// <c>Microsoft.EntityFrameworkCore.Database.Command</c> logs.
/// </summary>
/// <remarks>
/// Logging is strictly non-invasive: the helper only reads the SQL and the parameter
/// dictionary that already feed the Dapper call — it never mutates them, never creates
/// parameter objects, and never executes anything. A <see langword="null"/> logger (or
/// a logger with <see cref="LogLevel.Information"/> disabled) short-circuits to a no-op,
/// so disabled logging costs a single null check.
/// </remarks>
internal static class DapperSqlLog
{
    /// <summary>The logger category used for all Dapper SQL execution logs.</summary>
    internal const string CategoryName = "FlexQuery.NET.Dapper";

    private const string NullText = "NULL";

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
        sb.AppendLine(sql);
        sb.AppendLine();
        sb.AppendLine("Parameters:");

        var any = false;
        foreach (var parameter in parameters)
        {
            sb.Append(parameter.Key).Append(" = ").AppendLine(FormatValue(parameter.Value));
            any = true;
        }

        if (!any)
            sb.Append("(none)");

        return sb.ToString();
    }

    private static string FormatValue(object? value) => value switch
    {
        null or DBNull => NullText,
        _ => value.ToString() ?? NullText
    };
}
