using System.Collections.Concurrent;
using FlexQuery.NET.Dapper.Conventions;
using FlexQuery.NET.Dapper.Mapping.Builders;
using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Mapping;

/// <summary>
/// Registry for entity mappings with caching and convention-based discovery.
/// </summary>
public sealed class MappingRegistry : IMappingRegistry
{
    private readonly ConcurrentDictionary<Type, EntityMapping> _mappings = new();
    
    // Conventions
    private readonly IPluralizer _pluralizer;
    private readonly IEntityConvention _entityConvention;
    private readonly IRelationshipConvention _relationshipConvention;

    public MappingRegistry() : this(
        new DefaultPluralizer(), 
        new DefaultForeignKeyConvention())
    {
    }

    public MappingRegistry(IPluralizer pluralizer, IForeignKeyConvention foreignKeyConvention)
    {
        _pluralizer = pluralizer;
        _entityConvention = new DefaultEntityConvention(_pluralizer);
        _relationshipConvention = new DefaultRelationshipConvention(foreignKeyConvention);
    }

    /// <summary>Returns or creates the entity mapping for the given type.</summary>
    /// <summary>Returns or creates the entity mapping for the given type.</summary>
    public IEntityMapping GetMapping(Type entityType)
        => _mappings.GetOrAdd(entityType, CreateAndApplyConventions);

    public IEntityMapping GetMapping<T>() => GetMapping(typeof(T));

    /// <summary>Registers an existing entity mapping.</summary>
    public void Register(EntityMapping mapping) => _mappings[mapping.Type] = mapping;

    /// <summary>Returns a fluent builder for the given entity type.</summary>
    public EntityTypeBuilder<TEntity> Entity<TEntity>() where TEntity : class
    {
        var mapping = _mappings.GetOrAdd(typeof(TEntity), CreateAndApplyConventions);
        return new EntityTypeBuilder<TEntity>(mapping);
    }

    private EntityMapping CreateAndApplyConventions(Type entityType)
    {
        if (entityType == typeof(object))
        {
            throw new InvalidOperationException(
                "Entity type 'Object' is not a valid entity type. " +
                "Set EntityType in DapperQueryOptions or use a concrete type parameter " +
                "when calling FlexQueryAsync<T>().");
        }

        var mapping = new EntityMapping(entityType, entityType.Name);
        
        _entityConvention.Apply(mapping);
        _relationshipConvention.Apply(mapping, this);

        return mapping;
    }

    // For testing/internal configuration
    public IEnumerable<EntityMapping> GetAllMappings() => _mappings.Values;
}
