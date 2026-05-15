namespace FlexQuery.NET.Dapper.Mapping;

/// <summary>
/// Configuration for a database entity.
/// </summary>
public sealed class EntityMapping : IEntityMapping
{
    private readonly Dictionary<string, string> _propertyToColumn = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _columnToProperty = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, JoinInfo> _joins = new(StringComparer.OrdinalIgnoreCase);

    public Type Type { get; }
    public string TableName { get; }
    public string? TableAlias { get; }

    public EntityMapping(Type type, string tableName, string? tableAlias = null)
    {
        Type = type;
        TableName = tableName;
        TableAlias = tableAlias;
    }

    public void MapProperty(string property, string column)
    {
        _propertyToColumn[property] = column;
        _columnToProperty[column] = property;
    }

    public void MapJoin(string navigationProperty, Type targetType, string tableName, string joinCondition)
    {
        _joins[navigationProperty] = new JoinInfo
        {
            TargetType = targetType,
            TableName = tableName,
            JoinCondition = joinCondition
        };
    }

    public string GetColumnName(string propertyName)
        => _propertyToColumn.TryGetValue(propertyName, out var column) ? column : propertyName;

    public string? GetPropertyName(string columnName)
        => _columnToProperty.TryGetValue(columnName, out var property) ? property : null;

    public IEnumerable<string> GetProperties() => _propertyToColumn.Keys;

    public JoinInfo? GetJoinInfo(string navigationProperty)
        => _joins.TryGetValue(navigationProperty, out var joinInfo) ? joinInfo : null;
}
