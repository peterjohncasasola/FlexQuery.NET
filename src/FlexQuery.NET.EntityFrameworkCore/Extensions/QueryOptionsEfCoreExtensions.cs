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
    private const string EfCoreOperatorsMarker = "__EfCoreOperators";

    /// <summary>
    /// Registers EF Core-specific operator handlers (e.g., LIKE via EF.Functions.Like)
    /// on the current options instance and returns it for chaining.
    /// </summary>
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
