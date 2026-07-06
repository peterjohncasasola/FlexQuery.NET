using System.Reflection;

namespace FlexQuery.NET.Dapper.Mapping.Metadata;

/// <summary>
/// Configuration metadata for an entity property.
/// </summary>
internal sealed class PropertyMapping
{
    /// <summary>The reflection PropertyInfo for the mapped property.</summary>
    public PropertyInfo PropertyInfo { get; }
    /// <summary>The name of the property.</summary>
    public string PropertyName => PropertyInfo.Name;
    /// <summary>The column name in the database table.</summary>
    public string ColumnName { get; set; }
    /// <summary>Whether this property is the primary key.</summary>
    public bool IsPrimaryKey { get; set; }
    
    /// <summary>Creates a new property mapping for the given CLR property and database column name.</summary>
    public PropertyMapping(PropertyInfo propertyInfo, string columnName)
    {
        PropertyInfo = propertyInfo;
        ColumnName = columnName;
    }
}
