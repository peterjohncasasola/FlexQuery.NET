using System.Collections;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Conventions;

/// <summary>
/// Default relationship convention. Discovers relationships, infers foreign keys and target types.
/// </summary>
public class DefaultRelationshipConvention : IRelationshipConvention
{
    private readonly IForeignKeyConvention _foreignKeyConvention;

    public DefaultRelationshipConvention(IForeignKeyConvention foreignKeyConvention)
    {
        _foreignKeyConvention = foreignKeyConvention;
    }

    /// <summary>Applies relationship-level conventions: discovers navigation properties, infers foreign keys, and registers relationships.</summary>
    public void Apply(EntityMapping mapping, IMappingRegistry registry)
    {
        var type = mapping.Type;

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<NotMappedAttribute>() != null)
                continue;

            if (!IsNavigationProperty(property.PropertyType))
                continue;

            Type targetType;
            RelationshipType relType;

            if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType) && property.PropertyType != typeof(string))
            {
                // One-to-Many or Many-to-Many
                targetType = GetElementType(property.PropertyType);
                relType = RelationshipType.OneToMany; // We default to OneToMany, ManyToMany requires advanced detection
            }
            else
            {
                // Many-to-One or One-to-One
                targetType = property.PropertyType;
                relType = RelationshipType.ManyToOne;
            }

            var relMapping = mapping.GetOrAddRelationship(property, targetType, relType);

            // Infer Foreign Key
            var fkAttr = property.GetCustomAttribute<ForeignKeyAttribute>();
            if (fkAttr != null)
            {
                relMapping.ForeignKey = fkAttr.Name;
            }
            else if (string.IsNullOrEmpty(relMapping.ForeignKey))
            {
                relMapping.ForeignKey = _foreignKeyConvention.GetForeignKeyName(property, targetType, relType, type);
            }
        }
    }

    private Type GetElementType(Type type)
    {
        if (type.IsArray)
            return type.GetElementType()!;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return type.GetGenericArguments()[0];

        var enumerableInterface = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (enumerableInterface != null)
            return enumerableInterface.GetGenericArguments()[0];

        return typeof(object);
    }

    private bool IsNavigationProperty(Type type)
    {
        if (type == typeof(string) || type == typeof(byte[]) || type.IsValueType || type.IsPrimitive)
            return false;

        if (Nullable.GetUnderlyingType(type) != null)
            return false;

        return true; 
    }
}
