using System.Collections.Concurrent;
using System.Linq.Expressions;
using FlexQuery.NET.Constants;

namespace FlexQuery.NET.Operators;

/// <summary>
/// Global registry for operator handlers. Core registers safe defaults and
/// optional packages can override specific operators.
/// </summary>
public static class OperatorHandlerRegistry
{
    private static readonly ConcurrentDictionary<string, IOperatorHandler> Handlers =
        new(StringComparer.OrdinalIgnoreCase);

    static OperatorHandlerRegistry()
    {
        ResetToDefaults();
    }

    /// <summary>Registers or replaces a handler for its operator.</summary>
    public static void Register(IOperatorHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Handlers[handler.Operator] = handler;
    }

    /// <summary>Tries to get a handler for a canonical operator name.</summary>
    public static bool TryGet(string op, out IOperatorHandler? handler)
    {
        var ok = Handlers.TryGetValue(op, out var resolved);
        handler = resolved;
        return ok;
    }

    /// <summary>Resets handlers to the built-in core defaults.</summary>
    public static void ResetToDefaults()
    {
        Handlers.Clear();
        Register(new LikeFallbackOperatorHandler());
    }

    private sealed class LikeFallbackOperatorHandler : IOperatorHandler
    {
        public string Operator => FilterOperators.Like;

        public Expression? Build(Expression member, string? rawValue)
        {
            var underlying = Nullable.GetUnderlyingType(member.Type) ?? member.Type;
            if (underlying != typeof(string)) return null;

            var pattern = rawValue ?? string.Empty;
            var startsWithWildcard = pattern.StartsWith('%');
            var endsWithWildcard = pattern.EndsWith('%');
            var token = pattern.Trim('%');

            if (startsWithWildcard && endsWithWildcard)
                return CallString(member, nameof(string.Contains), token);
            if (startsWithWildcard)
                return CallString(member, nameof(string.EndsWith), token);
            if (endsWithWildcard)
                return CallString(member, nameof(string.StartsWith), token);

            // EF-agnostic fallback for non-wildcard patterns.
            return CallString(member, nameof(string.Contains), token);
        }

        private static Expression? CallString(Expression member, string methodName, string value)
        {
            var method = typeof(string).GetMethod(methodName, [typeof(string)]);
            if (method is null) return null;
            return Expression.Call(member, method, Expression.Constant(value, typeof(string)));
        }
    }
}
