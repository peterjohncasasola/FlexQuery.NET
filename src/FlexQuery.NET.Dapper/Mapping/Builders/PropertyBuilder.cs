using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Mapping.Builders;

/// <summary>
/// Fluent builder for configuring property column mappings.
/// </summary>
public sealed class PropertyBuilder
{
    private readonly PropertyMapping _mapping;

    internal PropertyBuilder(PropertyMapping mapping)
    {
        _mapping = mapping;
    }

    /// <summary>Sets the database column name for this property.</summary>
    public PropertyBuilder HasColumnName(string columnName)
    {
        _mapping.ColumnName = columnName;
        return this;
    }
}
