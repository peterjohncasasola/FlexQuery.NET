using System.Threading;
using FlexQuery.NET.EFCore.Operators;
using FlexQuery.NET.Models;
using FlexQuery.NET.Operators;

namespace FlexQuery.NET.EFCore;

/// <summary>
/// Opt-in registration for EF Core operator handlers.
/// </summary>
public static class QueryOptionsEfCoreExtensions
{
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
        OperatorHandlerRegistry.Register(new EfCoreLikeOperatorHandler());
    }
}
