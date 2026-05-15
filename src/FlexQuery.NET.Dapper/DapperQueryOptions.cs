using FlexQuery.NET.Models;
using FlexQuery.NET.Dapper.Sql;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Security;


namespace FlexQuery.NET.Dapper;

/// <summary>
/// Dapper-specific execution options that extends QueryExecutionOptions with SQL dialect configuration.
/// </summary>
public sealed class DapperQueryOptions : BaseQueryOptions
{
    /// <summary>
    /// Default constructor with Dapper-specific defaults.
    /// </summary>
    public DapperQueryOptions()
    {
        // Dapper defaults - override base IncludeTotalCount to false (Dapper behavior)
        IncludeTotalCount = false;
    }

    /// <summary>
    /// Copy constructor - creates a new instance by copying all properties from source.
    /// </summary>
    /// <param name="source">The source options to copy.</param>
    public DapperQueryOptions(QueryExecutionOptions source)
    {
        // Copy all properties from the base options
        MaxPageSize = source.MaxPageSize;
        DefaultPageSize = source.DefaultPageSize;
        CaseInsensitiveFields = source.CaseInsensitiveFields;
        IncludeTotalCount = source.IncludeTotalCount;
        StrictFieldValidation = source.StrictFieldValidation;
        MaxFieldDepth = source.MaxFieldDepth;
        AllowedFields = source.AllowedFields;
        BlockedFields = source.BlockedFields;
        AllowedIncludes = source.AllowedIncludes;
        ExpressionMappings = source.ExpressionMappings;
        FilterableFields = source.FilterableFields;
        SortableFields = source.SortableFields;
        SelectableFields = source.SelectableFields;

        // Dapper-specific defaults
        IncludeTotalCount = false; // Override to match Dapper behavior
    }

    public QueryExecutionOptions ToQueryExecutionOptions()
    {
        return new QueryExecutionOptions
        {
            MaxPageSize = this.MaxPageSize,
            DefaultPageSize = this.DefaultPageSize,
            CaseInsensitiveFields = this.CaseInsensitiveFields,
            IncludeTotalCount = this.IncludeTotalCount,
            StrictFieldValidation = this.StrictFieldValidation,
            MaxFieldDepth = this.MaxFieldDepth,
            AllowedFields = this.AllowedFields,
            BlockedFields = this.BlockedFields,
            AllowedIncludes = this.AllowedIncludes,
            ExpressionMappings = this.ExpressionMappings,
            FilterableFields = this.FilterableFields,
            SortableFields = this.SortableFields,
            SelectableFields = this.SelectableFields
        };
    }

    /// <summary>Global default SQL dialect. If set, overrides the automatic connection-based resolution for all queries unless a specific query provides its own dialect.</summary>
    public static ISqlDialect? GlobalDefaultDialect { get; set; }

    /// <summary>Global default resolver for SQL dialects. Defaults to DefaultSqlDialectResolver.</summary>
    public static ISqlDialectResolver GlobalDialectResolver { get; set; } = new DefaultSqlDialectResolver();

    /// <summary>SQL dialect to use for query generation. If null, resolves via GlobalDefaultDialect, then GlobalDialectResolver.</summary>
    public ISqlDialect? Dialect { get; set; }
    
    /// <summary>Command timeout in seconds.</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;
}
