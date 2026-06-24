using System.Threading;
using FlexQuery.NET.EntityFrameworkCore.Operators;
using FlexQuery.NET.Models;
using FlexQuery.NET.Operators;

namespace FlexQuery.NET.EntityFrameworkCore;

/// <summary>
/// Opt-in registration for EF Core operator handlers.
/// </summary>
public static class QueryOptionsEfCoreExtensions
{
    /// <summary>
    /// Registers EF Core-specific operator handlers and returns the same options instance.
    /// </summary>
    private const string EfCoreOperatorsMarker = "__EfCoreOperators";

    public static QueryOptions UseEfCoreOperators(this QueryOptions options)
    {
        EnsureEfCoreOperatorsRegistered();
        options.Items[EfCoreOperatorsMarker] = true;
        return options;
    }

    internal static void EnsureEfCoreOperatorsRegistered()
    {
        OperatorHandlerRegistry.Register(new EfCoreLikeOperatorHandler());
    }
}
