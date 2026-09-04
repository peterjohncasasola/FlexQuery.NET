namespace FlexQuery.NET.Mapping;

/// <summary>
/// Owns the <see cref="ITypeMap"/> graph for a query execution. One registry instance
/// is owned by the query configuration/options for the current execution and acts as
/// the single canonical source of mapping metadata.
/// </summary>
public interface IQueryMappingRegistry
{
    /// <summary>Finds a registered type map, or null when none exists.</summary>
    ITypeMap? Find(Type sourceType, Type destinationType);

    /// <summary>Finds or creates the type map for the given type pair.</summary>
    ITypeMap GetOrCreate(Type sourceType, Type destinationType);

    /// <summary>Finds or creates the strongly typed type map for the given type pair.</summary>
    ITypeMap GetOrCreate<TSource, TDestination>()
        where TSource : class
        where TDestination : class
        => GetOrCreate(typeof(TSource), typeof(TDestination));
}

/// <inheritdoc />
public sealed class QueryMappingRegistry : IQueryMappingRegistry
{
    private readonly object _lock = new();
    private readonly Dictionary<(Type Source, Type Destination), TypeMap> _maps = new();
    private readonly IQueryMappingRegistry? _fallback;

    /// <summary>
    /// Creates a registry. When <paramref name="fallback"/> is supplied, lookups that
    /// miss locally fall through to it — used to chain per-query registries to the
    /// application-level (global) registry so per-query registrations win.
    /// </summary>
    public QueryMappingRegistry(IQueryMappingRegistry? fallback = null)
    {
        _fallback = fallback;
    }

    /// <inheritdoc />
    public ITypeMap? Find(Type sourceType, Type destinationType)
    {
        lock (_lock)
        {
            if (_maps.TryGetValue((sourceType, destinationType), out var map))
                return map;
        }

        return _fallback?.Find(sourceType, destinationType);
    }

    /// <inheritdoc />
    public ITypeMap GetOrCreate(Type sourceType, Type destinationType)
    {
        lock (_lock)
        {
            if (_maps.TryGetValue((sourceType, destinationType), out var existing))
                return existing;

            var map = new TypeMap(sourceType, destinationType);
            _maps[(sourceType, destinationType)] = map;
            return map;
        }
    }

    /// <summary>Removes all registrations (infrastructure/test reset).</summary>
    internal void Clear() => _maps.Clear();
}
