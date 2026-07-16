using FlexQuery.NET.Models;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.EntityFrameworkCore.Includes;

/// <summary>
/// Applies flat navigation include paths from <see cref="QueryOptions.Includes"/>
/// to an EF Core query using the string-based <c>Include</c> API.
/// </summary>
internal static class IncludeBuilder
{
    /// <summary>
    /// Applies each include path in <paramref name="options"/> to <paramref name="query"/>.
    /// Returns <paramref name="query"/> unchanged when no includes are configured.
    /// </summary>
    public static IQueryable<T> Apply<T>(IQueryable<T> query, QueryOptions options)
        where T : class
    {
        if (options.Includes is not { Count: > 0 }) return query;

        foreach (var path in options.Includes)
            query = query.Include(path);

        return query;
    }
}
