using FlexQuery.NET.Constants;
using FlexQuery.NET.Options;
using Microsoft.AspNetCore.Http;

namespace FlexQuery.NET.AspNetCore.Extensions;

/// <summary>
/// Extension methods for <see cref="HttpContext"/> to support FlexQuery.NET server-side policy retrieval.
/// </summary>
public static class HttpContextExtensions
{
    private const string ExecutionOptionsKey = "FlexQueryExecutionOptions";

    /// <summary>
    /// Retrieves the <see cref="QueryExecutionOptions"/> configured for the current request, 
    /// typically populated by the [FieldAccess] action filter.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The configured execution options, or a new default instance if none was found.</returns>
    public static QueryExecutionOptions GetFlexQueryExecutionOptions(this HttpContext? context)
    {
        if (context == null)
            return new QueryExecutionOptions();

        if (context.Items.TryGetValue(ContextKeys.ExecutionOptions, out var optionsObj)
            && optionsObj is QueryExecutionOptions options)
        {
            return options;
        }

        // Backward-compatible fallback for callers that used the original
        // ASP.NET Core integration key directly.
        if (context.Items.TryGetValue(ExecutionOptionsKey, out optionsObj)
            && optionsObj is QueryExecutionOptions legacyOptions)
        {
            return legacyOptions;
        }

        return new QueryExecutionOptions();
    }
}
