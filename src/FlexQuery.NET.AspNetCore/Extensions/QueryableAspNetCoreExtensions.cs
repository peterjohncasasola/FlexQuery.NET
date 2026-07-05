using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Models;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FlexQuery.NET.AspNetCore.Extensions;

/// <summary>
/// ASP.NET Core specific extension methods for executing FlexQuery queries using server-owned execution options.
/// </summary>
public static class QueryableAspNetCoreExtensions
{
    /// <summary>
    /// Parses a <see cref="FlexQueryParameters"/>, resolves server-owned <see cref="QueryExecutionOptions"/> 
    /// from the <see cref="HttpContext"/>, and applies it to the query to return a paged result set asynchronously.
    /// </summary>
    /// <typeparam name="T">The entity type of the queryable source.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="parameters">The OpenAPI-friendly DTO containing user parameters.</param>
    /// <param name="context">The HTTP context containing the resolved execution options (populated by [FieldAccess]).</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation, containing the paged query result.</returns>
    /// <exception cref="OperationCanceledException">If the <see cref="CancellationToken" /> is canceled.</exception>
    public static async Task<QueryResult<object>> FlexQueryAsync<T>(
        this IQueryable<T> query,
        FlexQueryParameters parameters,
        HttpContext context,
        CancellationToken cancellationToken = default)
        where T : class
    {
        // 1. Resolve server-owned execution options
        var execOptions = context.GetFlexQueryExecutionOptions();

        // 2. Pass to the underlying EF Core integration
        return await query.FlexQueryAsync(parameters, execOptions, cancellationToken);
    }
}
