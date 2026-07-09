using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Configuration;

/// <summary>
/// Global application-wide FlexQuery defaults.
/// Configured once through DI and acts as the baseline execution behavior for all queries.
/// </summary>
public sealed class FlexQueryOptions
{
    /// <summary>
    /// The maximum page size that can be requested by clients.
    /// </summary>
    public int MaxPageSize { get; set; } = 1000;

    /// <summary>
    /// The default page size used when no page size is specified.
    /// </summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>
    /// Whether field name matching during validation is case-insensitive.
    /// </summary>
    public bool CaseInsensitive { get; set; } = true;

    /// <summary>
    /// Whether to include the total count in query results by default.
    /// </summary>
    public bool IncludeTotalCount { get; set; } = true;

    /// <summary>
    /// Whether to throw an exception when unauthorized fields are accessed.
    /// </summary>
    public bool StrictFieldValidation { get; set; } = true;

    /// <summary>
    /// The maximum depth of nested field paths allowed.
    /// </summary>
    public int MaxFieldDepth { get; set; } = 5;

    /// <summary>
    /// Gets or sets the default query syntax used when no explicit syntax is specified.
    /// Maps to <see cref="QueryOptionsParser.DefaultSyntax"/>.
    /// </summary>
    public QuerySyntax FilterSyntax
    {
        get => QueryOptionsParser.DefaultSyntax;
        set => QueryOptionsParser.DefaultSyntax = value;
    }
}
