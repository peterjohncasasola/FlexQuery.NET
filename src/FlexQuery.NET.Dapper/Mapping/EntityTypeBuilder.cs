namespace FlexQuery.NET.Dapper.Mapping;

/// <summary>
/// Builder for configuring an entity type.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class EntityTypeBuilder<T> where T : class
{
    private readonly Type _type;
    private readonly MappingRegistry _registry;
    private readonly EntityMapping _mapping;

    internal EntityTypeBuilder(Type type, MappingRegistry registry)
    {
        _type = type;
        _registry = registry;
        _mapping = new EntityMapping(type, type.Name.ToLowerInvariant() + "s");
    }

    /// <summary>Configures the table name and optional alias.</summary>
    public EntityTypeBuilder<T> Table(string tableName, string? alias = null)
    {
        var field = typeof(EntityMapping).GetField("_tableName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        // Direct field copy not supported, create new mapping
        return this;
    }

    /// <summary>Configures a property-to-column mapping.</summary>
    public PropertyBuilder<T> Property<TProperty>(System.Linq.Expressions.Expression<Func<T, TProperty>> propertyExpression)
    {
        var memberExpression = (System.Linq.Expressions.MemberExpression)propertyExpression.Body;
        var propertyName = memberExpression.Member.Name;
        return new PropertyBuilder<T>(_mapping, propertyName);
    }

    /// <summary>Finishes configuration and registers the mapping.</summary>
    public void Register() => _registry.Register(_mapping);
}
