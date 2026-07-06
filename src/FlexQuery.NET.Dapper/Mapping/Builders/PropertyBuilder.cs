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
    public PropertyBuilder HasColumn(string columnName)
    {
        _mapping.ColumnName = columnName;
        return this;
    }

    /// <summary>Marks this property as the primary key.</summary>
    public PropertyBuilder IsPrimaryKey()
    {
        _mapping.IsPrimaryKey = true;
        return this;
    }
}
