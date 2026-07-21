using System.Reflection;

namespace FlexQuery.NET.Dapper.Mapping.Metadata;

/// <summary>
/// Configuration metadata for a database entity.
/// </summary>
internal sealed class EntityMapping : IEntityMapping
{
    private readonly Dictionary<string, PropertyMapping> _properties = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RelationshipMapping> _relationships = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _ignoredProperties = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The CLR type of the entity.</summary>
    public Type Type { get; }
    /// <summary>The name of the database table.</summary>
    public string TableName { get; set; }
    /// <summary>The schema name.</summary>
    public string? Schema { get; set; }
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
        => _properties.GetValueOrDefault(propertyName);

    /// <summary>Gets the relationship mapping for the given navigation property name, or null if not found.</summary>
    public RelationshipMapping? GetRelationship(string navigationProperty)
        => _relationships.GetValueOrDefault(navigationProperty);

    /// <summary>All registered property mappings for this entity.</summary>
    public IEnumerable<PropertyMapping> Properties => _properties.Values;
    /// <summary>All registered relationship mappings for this entity.</summary>
    public IEnumerable<RelationshipMapping> Relationships => _relationships.Values;

    /// <summary>Property mappings marked as primary keys.</summary>
    public IReadOnlyList<PropertyMapping> Keys =>
        _properties.Values.Where(p => p.IsPrimaryKey).ToList();

    /// <summary>Marks the specified property as ignored.</summary>
    public void Ignore(string propertyName)
    {
        _ignoredProperties.Add(propertyName);
    }

    /// <summary>Checks whether the specified property is ignored.</summary>
    public bool IsIgnored(string propertyName) => _ignoredProperties.Contains(propertyName);

    /// <summary>Primary key property names.</summary>
    public IEnumerable<string> GetKeyProperties() => Keys.Select(k => k.PropertyName);

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
    public IEnumerable<string> GetProperties() => _properties.Keys.Except(_ignoredProperties, StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns join information for the given navigation property, or null if not found.</summary>
    public JoinInfo? GetJoinInfo(string navigationProperty)
    {
        if (_relationships.TryGetValue(navigationProperty, out var rel))
        {
            string joinCondition = rel.RelationshipType switch
            {
                RelationshipType.OneToMany => $"{TableName}.{RelationshipResolver.ResolvePrincipalColumn(this, rel)} = {rel.TargetType.Name}s.{rel.ForeignKey}",
                RelationshipType.ManyToOne => $"{TableName}.{rel.ForeignKey} = {rel.TargetType.Name}s.{RelationshipResolver.ResolvePrincipalColumn(this, rel)}",
                _ => string.Empty
            };

            return new JoinInfo
            {
                NavigationProperty = rel.NavigationPropertyName,
                TargetType = rel.TargetType,
                TableName = rel.TargetType.Name + "s",
                JoinCondition = joinCondition
            };
        }
        return null;
    }
}

