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

    /// <summary>Registers allowed fields for the specified type.</summary>
    public static void Register<T>(IEnumerable<string> fields)
        => _allowed[typeof(T)] = new HashSet<string>(fields, StringComparer.OrdinalIgnoreCase);

    /// <summary>Clears the allowed fields for the specified type.</summary>
    public static void Clear<T>()
        => _allowed.TryRemove(typeof(T), out _);

    /// <summary>Checks if a field is allowed for the specified entity type.</summary>
    public static bool IsAllowed(Type entityType, string fieldPath)
    {
        if (!_allowed.TryGetValue(entityType, out var whitelist))
            return true;

        return whitelist.Contains(fieldPath);
    }
}
