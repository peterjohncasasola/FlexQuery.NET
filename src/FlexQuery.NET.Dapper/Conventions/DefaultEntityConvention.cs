using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using FlexQuery.NET.Dapper.Mapping.Metadata;
using FlexQuery.NET.Helpers;

namespace FlexQuery.NET.Dapper.Conventions;

/// <summary>
/// Default entity convention. Infers table name, maps properties, detects primary key.
/// </summary>
internal class DefaultEntityConvention : IEntityConvention
{
    private readonly IPluralizer _pluralizer;

    /// <summary>
    /// Creates a new entity convention that uses the specified <paramref name="pluralizer"/>
    /// for table name generation.
    /// </summary>
    public DefaultEntityConvention(IPluralizer pluralizer)
    {
        _pluralizer = pluralizer;
    }

    /// <summary>Applies entity-level conventions (table name, column names, primary key detection) to the mapping.</summary>
    public void Apply(EntityMapping mapping)
    {
        var type = mapping.Type;

        // 1. Table Name Convention
        var tableAttr = type.GetCustomAttribute<TableAttribute>();
        if (tableAttr != null)
        {
            mapping.TableName = tableAttr.Name;
            mapping.Schema = string.IsNullOrEmpty(tableAttr.Schema) ? null : tableAttr.Schema;
        }
        else if (string.IsNullOrEmpty(mapping.TableName) || mapping.TableName == type.Name)
        {
            mapping.TableName = _pluralizer.Pluralize(type.Name);
        }

        // 2. Property Conventions
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (mapping.IsIgnored(property.Name))
                continue;

            if (property.GetCustomAttribute<NotMappedAttribute>() != null)
            {
                mapping.Ignore(property.Name);
                continue;
            }

            if (TypeHelper.IsNavigationProperty(property.PropertyType))
                continue;

            var propMapping = mapping.GetOrAddProperty(property);

            var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
            if (columnAttr != null)
            {
                propMapping.ColumnName = columnAttr.Name ?? property.Name;
            }

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
}
