using System.Collections.Concurrent;

namespace FlexQuery.NET.Caching;

/// <summary>
/// A thread-safe, size-bounded cache with FIFO eviction policy.
/// When the cache exceeds <see cref="FlexQueryCacheSettings.MaxCacheSize"/>,
/// the oldest entries are evicted to prevent unbounded memory growth.
/// </summary>
/// <typeparam name="TKey">The type of keys in the cache.</typeparam>
/// <typeparam name="TValue">The type of values in the cache.</typeparam>
internal sealed class BoundedConcurrentCache<TKey, TValue> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, TValue> _values = new();
    private readonly ConcurrentQueue<TKey> _insertionOrder = new();

    /// <summary>Gets the current number of entries in the cache.</summary>
    public int Count => _values.Count;

    /// <summary>Gets an existing value or creates and adds a new one using the factory.</summary>
    /// <param name="key">The key to look up or create.</param>
    /// <param name="factory">The factory function to create the value if not found.</param>
    /// <returns>The existing or newly created value.</returns>
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
    {
        return _values.GetOrAdd(key, k =>
        {
            var created = factory(k);
            _insertionOrder.Enqueue(k);
            Trim();
            return created;
        });
    }

    /// <summary>Attempts to retrieve a value by key.</summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">When this method returns, contains the value if found.</param>
    /// <returns>true if the key was found; otherwise, false.</returns>
    public bool TryGetValue(TKey key, out TValue? value)
        => _values.TryGetValue(key, out value);

    /// <summary>Sets a value for the specified key, adding or overwriting as needed.</summary>
    /// <param name="key">The key to set.</param>
    /// <param name="value">The value to store.</param>
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

    /// <summary>Clears all entries from the cache.</summary>
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

