namespace FlexQuery.NET.Mapping;

public interface IQueryMappingRegistry
{
    ITypeMap? Find(Type sourceType, Type destinationType);
    ITypeMap GetOrCreate(Type sourceType, Type destinationType);

    ITypeMap GetOrCreate<TSource, TDestination>()
        where TSource : class
        where TDestination : class
        => GetOrCreate(typeof(TSource), typeof(TDestination));
}

public sealed class QueryMappingRegistry : IQueryMappingRegistry
{
    private readonly object _lock = new();
    private readonly Dictionary<(Type Source, Type Destination), TypeMap> _maps = new();
    private readonly IQueryMappingRegistry? _fallback;
    private bool _frozen;

    public QueryMappingRegistry(IQueryMappingRegistry? fallback = null)
    {
        _fallback = fallback;
    }

    public ITypeMap? Find(Type sourceType, Type destinationType)
    {
        lock (_lock)
        {
            if (_maps.TryGetValue((sourceType, destinationType), out var map))
                return map;
        }

        return _fallback?.Find(sourceType, destinationType);
    }

    public ITypeMap GetOrCreate(Type sourceType, Type destinationType)
    {
        lock (_lock)
        {
            if (_maps.TryGetValue((sourceType, destinationType), out var existing))
                return existing;

            if (_frozen)
                throw new InvalidOperationException("FlexQuery has already executed queries; configuration must happen during startup.");

            var map = new TypeMap(sourceType, destinationType);
            _maps[(sourceType, destinationType)] = map;
            return map;
        }
    }

    internal void Freeze()
    {
        lock (_lock)
        {
            if (_frozen)
                return;

            _frozen = true;
            foreach (var map in _maps.Values)
                map.Freeze();
        }
    }

    internal void Clear()
    {
        lock (_lock)
        {
            if (_frozen)
                throw new InvalidOperationException("Cannot clear a frozen FlexQuery mapping registry.");
            _maps.Clear();
        }
    }
}
