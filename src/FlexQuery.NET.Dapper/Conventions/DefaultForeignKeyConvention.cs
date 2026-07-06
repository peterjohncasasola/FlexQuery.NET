using System.Reflection;
using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Conventions;

/// <summary>
/// Default convention for inferring foreign key column names.
/// </summary>
internal class DefaultForeignKeyConvention : IForeignKeyConvention
{
    /// <summary>Infers the foreign key column name for the given navigation property and relationship type.</summary>
    public string GetForeignKeyName(PropertyInfo navigationProperty, Type targetType, RelationshipType relationshipType, Type entityType)
    {
        if (relationshipType == RelationshipType.OneToMany)
        {
            // For Customer.Orders, the FK is on Order, pointing to Customer.
            // FK is usually CustomerId.
            return entityType.Name + "Id";
        }
        else if (relationshipType == RelationshipType.ManyToOne)
        {
            // For Order.Customer, the FK is on Order, pointing to Customer.
            // FK is usually CustomerId.
            return navigationProperty.Name + "Id";
        }
        
        return navigationProperty.Name + "Id";
    }
}
