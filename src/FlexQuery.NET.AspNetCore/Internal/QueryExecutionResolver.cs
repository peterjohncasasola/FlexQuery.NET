using FlexQuery.NET.Internal;
using FlexQuery.NET.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.AspNetCore.Internal;

/// <summary>
/// Resolves effective query options for the current request.
/// </summary>
internal static class QueryExecutionResolver
{
    /// <summary>
    /// Resolves the effective query options for the current request by merging global <see cref="FlexQueryOptions"/>
    /// with per-request <see cref="QueryExecutionOptions"/>.
    /// </summary>
    /// <param name="httpContext">The current HTTP context from which global options are resolved.</param>
    /// <param name="execution">Optional per-request execution options (e.g. populated by <c>[FieldAccess]</c>).</param>
    /// <returns>An <see cref="EffectiveQueryOptions"/> combining global and per-request settings.</returns>
    internal static EffectiveQueryOptions Resolve(
        HttpContext httpContext,
        QueryExecutionOptions? execution)
    {
        var global = httpContext
            .RequestServices
            .GetRequiredService<FlexQueryOptions>();

        return EffectiveQueryOptionsFactory.Create(
            global,
            execution);
    }
}