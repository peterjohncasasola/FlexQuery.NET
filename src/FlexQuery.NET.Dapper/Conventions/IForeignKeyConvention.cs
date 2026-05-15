using System.Reflection;
using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Conventions;

/// <summary>
/// Convention for inferring foreign key column names.
/// </summary>
public interface IForeignKeyConvention
{
    string GetForeignKeyName(PropertyInfo navigationProperty, Type targetType, RelationshipType relationshipType, Type entityType);
}
