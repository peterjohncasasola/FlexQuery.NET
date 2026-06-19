namespace FlexQuery.NET.Dapper.Sql;

/// <summary>
/// Result of SQL translation containing the SQL string and parameters.
/// </summary>
public sealed class SqlCommand
{
    /// <summary>The generated SQL string.</summary>
    public string Sql { get; init; } = string.Empty;

    /// <summary>The parameters for the SQL command.</summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();

    /// <summary>Creates an empty SQL command.</summary>
    public static SqlCommand Empty { get; } = new() { Sql = string.Empty };
}
