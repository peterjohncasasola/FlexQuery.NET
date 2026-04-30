using System.Threading;
using DynamicQueryable.Extensions.EFCore.Operators;
using DynamicQueryable.Models;
using DynamicQueryable.Operators;

namespace DynamicQueryable.Extensions.EFCore;

/// <summary>
/// Opt-in registration for EF Core operator handlers.
/// </summary>
public static class QueryOptionsEfCoreExtensions
{
    private static int _isRegistered;

    /// <summary>
    /// Registers EF Core-specific operator handlers and returns the same options instance.
    /// </summary>
    public static QueryOptions UseEfCoreOperators(this QueryOptions options)
    {
        EnsureEfCoreOperatorsRegistered();
        return options;
    }

    internal static void EnsureEfCoreOperatorsRegistered()
    {
        if (Interlocked.Exchange(ref _isRegistered, 1) == 1) return;
        OperatorHandlerRegistry.Register(new EfCoreLikeOperatorHandler());
    }
}
