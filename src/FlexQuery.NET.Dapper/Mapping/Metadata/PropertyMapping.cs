using System.Reflection;

namespace FlexQuery.NET.Dapper.Mapping.Metadata;

/// <summary>
/// Configuration metadata for an entity property.
/// </summary>
public sealed class PropertyMapping
{
    public PropertyInfo PropertyInfo { get; }
    public string PropertyName => PropertyInfo.Name;
    public string ColumnName { get; set; }
    public bool IsPrimaryKey { get; set; }
    
    public PropertyMapping(PropertyInfo propertyInfo, string columnName)
    {
        PropertyInfo = propertyInfo;
        ColumnName = columnName;
    }
}
