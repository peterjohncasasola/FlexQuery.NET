using System.Data;
using System.Data.Common;

namespace FlexQuery.NET.Dapper.Sql.Utilities;

internal static class ConnectionHelper
{
    public static Task EnsureOpenAsync(DbConnection connection, CancellationToken ct)
    {
        return connection.State == ConnectionState.Open
            ? Task.CompletedTask
            : connection.OpenAsync(ct);
    }
}