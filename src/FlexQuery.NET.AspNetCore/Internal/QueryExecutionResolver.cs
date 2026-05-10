using FlexQuery.NET.Configuration;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FlexQuery.NET.AspNetCore.Internal;

/// <summary>
/// Resolves effective query options for the current request.
/// </summary>
internal static class QueryExecutionResolver
{
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