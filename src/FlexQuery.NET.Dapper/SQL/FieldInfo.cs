namespace FlexQuery.NET.Dapper.Sql;

/// <summary>
/// Information about a translated field.
/// </summary>
public sealed class FieldInfo
{
    /// <summary>Property name in the entity.</summary>
    public string PropertyName { get; init; } = string.Empty;

    /// <summary>Column name in the database.</summary>
    public string ColumnName { get; init; } = string.Empty;

    /// <summary>Table alias.</summary>
    public string? TableAlias { get; init; }

    /// <summary>Full qualified column name (alias.column).</summary>
    public string QualifiedName => string.IsNullOrEmpty(TableAlias) ? ColumnName : $"{TableAlias}.{ColumnName}";
}
