using FlexQuery.NET.Models;
using FlexQuery.NET.QuerySurface;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Projection;

namespace FlexQuery.NET.EntityFrameworkCore;

/// <summary>
/// Builds and applies a concrete DTO projection to an EF Core query.
/// </summary>
internal static class DtoProjectionEfCoreExtensions
{
    /// <summary>
    /// Projects <paramref name="query"/> into <typeparamref name="TResponse"/> using the
    /// fields resolved by <paramref name="surface"/>. <paramref name="queryOptions"/> determines
    /// which fields are selected; if empty, <paramref name="surface"/>'s default select fields are used.
    /// </summary>
    public static IQueryable<TResponse> ApplyDtoSelect<TEntity, TResponse>(
        this IQueryable<TEntity> query,
        QueryOptions queryOptions,
        IQuerySurface surface,
        IReadOnlyList<SelectNode>? fieldsOverride = null,
        Mapping.IQueryMappingRegistry? mappingRegistry = null)
        where TEntity : class
        where TResponse : class
    {
        var lambda = DtoProjectionBuilder.Build<TEntity, TResponse>(
            queryOptions, surface, fieldsOverride, mappingRegistry: mappingRegistry);
        return query.Select(lambda);
    }
}
