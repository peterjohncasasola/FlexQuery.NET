using System.Reflection;
using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Conventions;

/// <summary>
/// Convention for inferring foreign key column names.
/// </summary>
internal interface IForeignKeyConvention
{
    /// <summary>Returns the inferred foreign key column name for a navigation property.</summary>
    string GetForeignKeyName(PropertyInfo navigationProperty, Type targetType, RelationshipType relationshipType, Type entityType);
}
