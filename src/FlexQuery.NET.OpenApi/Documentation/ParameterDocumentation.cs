namespace FlexQuery.NET.OpenApi.Documentation;

internal static class ParameterDocumentation
{
    internal static readonly string Filter =
        "A filter expression using FlexQuery DSL syntax. Format: field:operator:value (e.g., Status:eq:Active).";

    internal static readonly string Sort =
        "A sort expression. Format: field:direction (e.g., LastName:asc).";

    internal static readonly string Page =
        "1-based page number for pagination.";

    internal static readonly string PageSize =
        "Number of items per page (default: 20, max: 1000).";

    internal static readonly string Select =
        "Comma-separated field paths to include in the result (e.g., Id,FirstName,Email).";

    internal static readonly string Include =
        "Comma-separated navigation property paths to include (e.g., Orders,Profile).";
}
