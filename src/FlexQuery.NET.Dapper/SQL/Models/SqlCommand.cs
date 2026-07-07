namespace FlexQuery.NET.Dapper.Sql.Models;

/// <summary>
/// Result of SQL translation containing the SQL string and parameters.
/// </summary>
internal sealed class SqlCommand
{
    /// <summary>The generated SQL string.</summary>
    public string Sql { get; init; } = string.Empty;

    /// <summary>The parameters for the SQL command.</summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();

    /// <summary>List of navigation paths used for flat projection (Flat/FlatMixed mode).</summary>
    public List<string>? FlatJoins { get; init; }

    /// <summary>Gets a singleton, empty SQL command with no SQL text and no parameters.</summary>
    public static SqlCommand Empty { get; } = new() { Sql = string.Empty };
}
