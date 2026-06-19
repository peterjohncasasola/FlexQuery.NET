using System.Collections.Concurrent;

namespace FlexQuery.NET.Caching;

internal sealed class BoundedConcurrentCache<TKey, TValue> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, TValue> _values = new();
    private readonly ConcurrentQueue<TKey> _insertionOrder = new();

    public int Count => _values.Count;

    public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
    {
        if (_values.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var value = _values.GetOrAdd(key, k =>
        {
            var created = factory(k);
            _insertionOrder.Enqueue(k);
            return created;
        });

        Trim();
        return value;
    }

    public bool TryGetValue(TKey key, out TValue? value)
        => _values.TryGetValue(key, out value);

    public void Set(TKey key, TValue value)
    {
        var added = _values.TryAdd(key, value);
        if (!added)
        {
            _values[key] = value;
        }
        else
        {
            _insertionOrder.Enqueue(key);
        }

        Trim();
    }

    public void Clear()
    {
        _values.Clear();
        while (_insertionOrder.TryDequeue(out _)) { }
    }

    private void Trim()
    {
        var max = Math.Max(1, FlexQueryCacheSettings.MaxCacheSize);
        while (_values.Count > max && _insertionOrder.TryDequeue(out var key))
        {
            _values.TryRemove(key, out _);
        }
    }
}
