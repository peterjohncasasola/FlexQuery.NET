namespace FlexQuery.NET.Dapper.Mapping;

/// <summary>
/// Builder for configuring a property mapping.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public sealed class PropertyBuilder<TEntity>
{
    private readonly EntityMapping _mapping;
    private readonly string _propertyName;

    internal PropertyBuilder(EntityMapping mapping, string propertyName)
    {
        _mapping = mapping;
        _propertyName = propertyName;
    }

    /// <summary>Specifies the database column name.</summary>
    public PropertyBuilder<TEntity> HasColumn(string columnName)
    {
        _mapping.MapProperty(_propertyName, columnName);
        return this;
    }
}
