using System.Collections.Concurrent;

namespace DynamicQueryable.Security;

/// <summary>
/// Optional per-entity field whitelist for secure dynamic filtering.
/// If an entity type has no registered fields, all fields are allowed.
/// </summary>
public static class FieldRegistry
{
    private static readonly ConcurrentDictionary<Type, HashSet<string>> _allowed =
        new();

    public static void Register<T>(IEnumerable<string> fields)
        => _allowed[typeof(T)] = new HashSet<string>(fields, StringComparer.OrdinalIgnoreCase);

    public static void Clear<T>()
        => _allowed.TryRemove(typeof(T), out _);

    public static bool IsAllowed(Type entityType, string fieldPath)
    {
        if (!_allowed.TryGetValue(entityType, out var whitelist))
            return true;

        return whitelist.Contains(fieldPath);
    }
}
