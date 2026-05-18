using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Conventions;

/// <summary>
/// Default entity convention. Infers table name, maps properties, detects primary key.
/// </summary>
public class DefaultEntityConvention : IEntityConvention
{
    private readonly IPluralizer _pluralizer;

    public DefaultEntityConvention(IPluralizer pluralizer)
    {
        _pluralizer = pluralizer;
    }

    public void Apply(EntityMapping mapping)
    {
        var type = mapping.Type;

        // 1. Table Name Convention
        var tableAttr = type.GetCustomAttribute<TableAttribute>();
        if (tableAttr != null)
        {
            mapping.TableName = tableAttr.Name;
        }
        else if (string.IsNullOrEmpty(mapping.TableName) || mapping.TableName == type.Name)
        {
            mapping.TableName = _pluralizer.Pluralize(type.Name);
        }

        // 2. Property Conventions
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // Skip unmapped / ignored properties (can add NotMappedAttribute support here)
            if (property.GetCustomAttribute<NotMappedAttribute>() != null)
                continue;

            // Skip navigation properties (complex types and collections are handled by relationship convention)
            if (IsNavigationProperty(property.PropertyType))
                continue;

            var propMapping = mapping.GetOrAddProperty(property);

            // Column Name
            var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
            if (columnAttr != null)
            {
                propMapping.ColumnName = columnAttr.Name ?? property.Name;
            }

            // Primary Key
            if (property.GetCustomAttribute<KeyAttribute>() != null)
            {
                propMapping.IsPrimaryKey = true;
            }
            else if (string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(property.Name, type.Name + "Id", StringComparison.OrdinalIgnoreCase))
            {
                propMapping.IsPrimaryKey = true;
            }
        }
    }

    private bool IsNavigationProperty(Type type)
    {
        if (type == typeof(string) || type == typeof(byte[]) || type.IsValueType || type.IsPrimitive)
            return false;

        // Nullable<T> where T is a value type is not a navigation property
        if (Nullable.GetUnderlyingType(type) != null)
            return false;

        return true; 
    }
}
