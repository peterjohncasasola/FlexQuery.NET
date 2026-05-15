namespace FlexQuery.NET.Dapper.Mapping;

/// <summary>
/// Entry point for configuring entity mappings.
/// </summary>
public sealed class EntityMappingBuilder
{
    private readonly MappingRegistry _registry;

    internal EntityMappingBuilder(MappingRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>Configures an entity type.</summary>
    public EntityTypeBuilder<T> Entity<T>() where T : class
        => new(typeof(T), _registry);
}
