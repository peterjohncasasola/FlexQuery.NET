using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Mapping.Builders;

public class PropertyBuilder
{
    private readonly PropertyMapping _mapping;

    public PropertyBuilder(PropertyMapping mapping)
    {
        _mapping = mapping;
    }

    public PropertyBuilder HasColumn(string columnName)
    {
        _mapping.ColumnName = columnName;
        return this;
    }

    public PropertyBuilder IsPrimaryKey()
    {
        _mapping.IsPrimaryKey = true;
        return this;
    }
}
