using FlexQuery.NET.EntityFrameworkCore;

namespace FlexQuery.NET.EntityFrameworkCore;

/// <summary>
/// Provides explicit initialization methods for FlexQuery Entity Framework Core integration,
/// without dependency injection.
/// </summary>
public static class FlexQueryEFCore
{
    /// <summary>
    /// Ensures EF Core-specific query operators are registered.
    /// Must be called once during application startup.
    /// </summary>
    public static void Setup()
    {
        QueryOptionsEfCoreExtensions.EnsureEfCoreOperatorsRegistered();
    }
}
