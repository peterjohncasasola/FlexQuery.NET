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

    /// <summary>Fully qualified column name in the form "Alias.Column" or just "Column" when no alias is set.</summary>
    public string QualifiedName => string.IsNullOrEmpty(TableAlias) ? ColumnName : $"{TableAlias}.{ColumnName}";
}
