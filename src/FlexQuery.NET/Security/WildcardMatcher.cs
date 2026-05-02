using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace FlexQuery.NET.Security;

/// <summary>
/// Helper class for matching field paths against wildcard patterns with regex caching.
/// </summary>
internal static class WildcardMatcher
{
    private static readonly ConcurrentDictionary<string, Regex> _regexCache = new();

    /// <summary>
    /// Checks if a field matches any of the provided patterns (supporting * wildcards).
    /// </summary>
    public static bool IsMatch(string field, IEnumerable<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (pattern == "*" || string.Equals(pattern, field, StringComparison.OrdinalIgnoreCase)) return true;
            if (!pattern.Contains('*')) continue;

            var regex = _regexCache.GetOrAdd(pattern, p => 
                new Regex("^" + Regex.Escape(p).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase | RegexOptions.Compiled));

            if (regex.IsMatch(field)) return true;
        }

        return false;
    }
}
