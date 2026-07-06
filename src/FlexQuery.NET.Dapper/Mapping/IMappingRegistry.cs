using FlexQuery.NET.Dapper.Mapping.Builders;

namespace FlexQuery.NET.Dapper.Mapping;

/// <summary>
/// Registry for entity mappings.
/// </summary>
internal interface IMappingRegistry
{
    /// <summary>Gets the mapping for an entity type.</summary>
    IEntityMapping GetMapping(Type entityType);

    /// <summary>Gets the mapping for an entity type.</summary>
    IEntityMapping GetMapping<T>();

    /// <summary>Configures an entity mapping using the fluent builder API.</summary>
    EntityTypeBuilder<TEntity> Entity<TEntity>() where TEntity : class;
}
