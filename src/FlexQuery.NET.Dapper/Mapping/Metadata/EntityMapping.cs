using System.Reflection;

namespace FlexQuery.NET.Dapper.Mapping.Metadata;

/// <summary>
/// Configuration metadata for a database entity.
/// </summary>
public sealed class EntityMapping : IEntityMapping
{
    private readonly Dictionary<string, PropertyMapping> _properties = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RelationshipMapping> _relationships = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The CLR type of the entity.</summary>
    public Type Type { get; }
    /// <summary>The name of the database table.</summary>
    public string TableName { get; set; }
    /// <summary>An optional alias for the table used in SQL queries.</summary>
    public string? TableAlias { get; set; }

    /// <summary>Creates a new entity mapping for the given type and table name.</summary>
    public EntityMapping(Type type, string tableName, string? tableAlias = null)
    {
        Type = type;
        TableName = tableName;
        TableAlias = tableAlias;
    }

    /// <summary>Returns the existing <see cref="PropertyMapping"/> for the given property, or creates and adds a new one.</summary>
    public PropertyMapping GetOrAddProperty(PropertyInfo propertyInfo)
    {
        if (!_properties.TryGetValue(propertyInfo.Name, out var mapping))
        {
            mapping = new PropertyMapping(propertyInfo, propertyInfo.Name);
            _properties[propertyInfo.Name] = mapping;
        }
        return mapping;
    }

    /// <summary>Returns the existing <see cref="RelationshipMapping"/> for the given navigation property, or creates and adds a new one.</summary>
    public RelationshipMapping GetOrAddRelationship(PropertyInfo propertyInfo, Type targetType, RelationshipType type)
    {
        if (!_relationships.TryGetValue(propertyInfo.Name, out var mapping))
        {
            mapping = new RelationshipMapping(propertyInfo, targetType, type);
            _relationships[propertyInfo.Name] = mapping;
        }
        return mapping;
    }

    /// <summary>Gets the property mapping for the given property name, or null if not found.</summary>
    public PropertyMapping? GetProperty(string propertyName)
        => _properties.TryGetValue(propertyName, out var p) ? p : null;

    /// <summary>Gets the relationship mapping for the given navigation property name, or null if not found.</summary>
    public RelationshipMapping? GetRelationship(string navigationProperty)
        => _relationships.TryGetValue(navigationProperty, out var r) ? r : null;

    /// <summary>All registered property mappings for this entity.</summary>
    public IEnumerable<PropertyMapping> Properties => _properties.Values;
    /// <summary>All registered relationship mappings for this entity.</summary>
    public IEnumerable<RelationshipMapping> Relationships => _relationships.Values;

    // --- Backward compatibility with existing IEntityMapping interface ---

    /// <summary>Returns the database column name for the given property name.</summary>
    public string GetColumnName(string propertyName)
    {
        if (_properties.TryGetValue(propertyName, out var p))
            return p.ColumnName;
        return propertyName;
    }

    /// <summary>Returns the property name for the given database column name, or null if not found.</summary>
    public string? GetPropertyName(string columnName)
    {
        var prop = _properties.Values.FirstOrDefault(p => p.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));
        return prop?.PropertyName;
    }

    /// <summary>Returns all registered property names for this entity.</summary>
    public IEnumerable<string> GetProperties() => _properties.Keys;

    /// <summary>Returns join information for the given navigation property, or null if not found.</summary>
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
