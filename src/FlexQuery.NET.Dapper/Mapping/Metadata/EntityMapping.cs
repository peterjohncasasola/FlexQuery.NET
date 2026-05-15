using System.Reflection;

namespace FlexQuery.NET.Dapper.Mapping.Metadata;

/// <summary>
/// Configuration metadata for a database entity.
/// </summary>
public sealed class EntityMapping : IEntityMapping
{
    private readonly Dictionary<string, PropertyMapping> _properties = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RelationshipMapping> _relationships = new(StringComparer.OrdinalIgnoreCase);

    public Type Type { get; }
    public string TableName { get; set; }
    public string? TableAlias { get; set; }

    public EntityMapping(Type type, string tableName, string? tableAlias = null)
    {
        Type = type;
        TableName = tableName;
        TableAlias = tableAlias;
    }

    public PropertyMapping GetOrAddProperty(PropertyInfo propertyInfo)
    {
        if (!_properties.TryGetValue(propertyInfo.Name, out var mapping))
        {
            mapping = new PropertyMapping(propertyInfo, propertyInfo.Name);
            _properties[propertyInfo.Name] = mapping;
        }
        return mapping;
    }

    public RelationshipMapping GetOrAddRelationship(PropertyInfo propertyInfo, Type targetType, RelationshipType type)
    {
        if (!_relationships.TryGetValue(propertyInfo.Name, out var mapping))
        {
            mapping = new RelationshipMapping(propertyInfo, targetType, type);
            _relationships[propertyInfo.Name] = mapping;
        }
        return mapping;
    }

    public PropertyMapping? GetProperty(string propertyName)
        => _properties.TryGetValue(propertyName, out var p) ? p : null;

    public RelationshipMapping? GetRelationship(string navigationProperty)
        => _relationships.TryGetValue(navigationProperty, out var r) ? r : null;

    public IEnumerable<PropertyMapping> Properties => _properties.Values;
    public IEnumerable<RelationshipMapping> Relationships => _relationships.Values;

    // --- Backward compatibility with existing IEntityMapping interface ---

    public string GetColumnName(string propertyName)
    {
        if (_properties.TryGetValue(propertyName, out var p))
            return p.ColumnName;
        return propertyName;
    }

    public string? GetPropertyName(string columnName)
    {
        var prop = _properties.Values.FirstOrDefault(p => p.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));
        return prop?.PropertyName;
    }

    public IEnumerable<string> GetProperties() => _properties.Keys;

    public JoinInfo? GetJoinInfo(string navigationProperty)
    {
        // JoinInfo is a legacy structure, we construct it dynamically if needed by the translators.
        if (_relationships.TryGetValue(navigationProperty, out var rel))
        {
            // The actual join condition is usually built during translation based on the relationship type.
            // But for backward compatibility with existing tests/translators, we can generate a basic condition.
            string joinCondition = rel.RelationshipType switch
            {
                RelationshipType.OneToMany => $"{TableName}.Id = {rel.TargetType.Name}s.{rel.ForeignKey}",
                RelationshipType.ManyToOne => $"{TableName}.{rel.ForeignKey} = {rel.TargetType.Name}s.{rel.PrincipalKey}",
                _ => string.Empty
            };

            return new JoinInfo
            {
                NavigationProperty = rel.NavigationPropertyName,
                TargetType = rel.TargetType,
                // Ideally, TableName for target should be retrieved from the MappingRegistry,
                // but we might not have it here. The translator will need to fetch it.
                // We provide a fallback for now.
                TableName = rel.TargetType.Name + "s", 
                JoinCondition = joinCondition
            };
        }
        return null;
    }
}
