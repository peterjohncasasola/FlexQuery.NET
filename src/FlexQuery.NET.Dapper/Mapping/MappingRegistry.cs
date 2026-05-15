using System.Collections.Concurrent;

namespace FlexQuery.NET.Dapper.Mapping;

/// <summary>
/// Registry for entity mappings with caching.
/// </summary>
public sealed class MappingRegistry : IMappingRegistry
{
    private readonly ConcurrentDictionary<Type, IEntityMapping> _mappings = new();

    public IEntityMapping GetMapping(Type entityType)
        => _mappings.GetOrAdd(entityType, _ => CreateDefaultMapping(entityType));

    public IEntityMapping GetMapping<T>() => GetMapping(typeof(T));

    public void Register(IEntityMapping mapping) => _mappings[mapping.Type] = mapping;

    private IEntityMapping CreateDefaultMapping(Type entityType)
    {
        var tableName = entityType.Name;
        if (tableName.EndsWith("Entity", StringComparison.OrdinalIgnoreCase))
            tableName = tableName.Substring(0, tableName.Length - 6);
        if (tableName.EndsWith("Dto", StringComparison.OrdinalIgnoreCase))
            tableName = tableName.Substring(0, tableName.Length - 3);
        tableName = tableName.ToLowerInvariant() + "s";

        return new EntityMapping(entityType, tableName);
    }
}
