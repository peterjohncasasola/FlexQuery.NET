using System.Collections.Concurrent;
using FlexQuery.NET.Dapper.Conventions;
using FlexQuery.NET.Dapper.Mapping.Builders;
using FlexQuery.NET.Dapper.Mapping.Metadata;

namespace FlexQuery.NET.Dapper.Mapping;

/// <summary>
/// Registry for entity mappings with caching and convention-based discovery.
/// </summary>
internal sealed class MappingRegistry : IMappingRegistry
{
    private readonly ConcurrentDictionary<Type, EntityMapping> _mappings = new();
    
    // Conventions
    private readonly IPluralizer _pluralizer;
    private readonly IEntityConvention _entityConvention;
    private readonly IRelationshipConvention _relationshipConvention;

    /// <summary>Initializes a new instance with default conventions.</summary>
    public MappingRegistry() : this(
        new DefaultPluralizer(), 
        new DefaultForeignKeyConvention())
    {
    }

    /// <summary>Initializes a new instance with custom pluralizer and foreign key convention.</summary>
    /// <param name="pluralizer">The pluralizer for entity/table name conventions.</param>
    /// <param name="foreignKeyConvention">The foreign key naming convention.</param>
    public MappingRegistry(IPluralizer pluralizer, IForeignKeyConvention foreignKeyConvention)
    {
        _pluralizer = pluralizer;
        _entityConvention = new DefaultEntityConvention(_pluralizer);
        _relationshipConvention = new DefaultRelationshipConvention(foreignKeyConvention);
    }

    /// <summary>Returns or creates the entity mapping for the given type.</summary>
    public IEntityMapping GetMapping(Type entityType)
        => _mappings.GetOrAdd(entityType, CreateAndApplyConventions);

    /// <summary>Returns or creates the entity mapping for the type <typeparamref name="T"/>.</summary>
    public IEntityMapping GetMapping<T>() => GetMapping(typeof(T));

    /// <summary>Registers an existing entity mapping, overwriting any previously registered mapping for the same type.</summary>
    internal void Register(EntityMapping mapping) => _mappings[mapping.Type] = mapping;

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

    /// <summary>Returns all registered entity mappings. Intended for testing and internal diagnostics.</summary>
    internal IEnumerable<EntityMapping> GetAllMappings() => _mappings.Values;
}
