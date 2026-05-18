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

    public IEntityMapping GetMapping(Type entityType)
        => _mappings.GetOrAdd(entityType, CreateAndApplyConventions);

    public IEntityMapping GetMapping<T>() => GetMapping(typeof(T));

    public void Register(EntityMapping mapping) => _mappings[mapping.Type] = mapping;

    public EntityTypeBuilder<TEntity> Entity<TEntity>() where TEntity : class
    {
        var mapping = _mappings.GetOrAdd(typeof(TEntity), CreateAndApplyConventions);
        return new EntityTypeBuilder<TEntity>(mapping);
    }

    private EntityMapping CreateAndApplyConventions(Type entityType)
    {
        var mapping = new EntityMapping(entityType, entityType.Name);
        
        _entityConvention.Apply(mapping);
        _relationshipConvention.Apply(mapping, this);

        return mapping;
    }

    // For testing/internal configuration
    public IEnumerable<EntityMapping> GetAllMappings() => _mappings.Values;
}
