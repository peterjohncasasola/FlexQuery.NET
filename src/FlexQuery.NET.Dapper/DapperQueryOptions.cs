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
        // Dapper defaults
        IncludeTotalCount = true;
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
        IncludeTotalCount = true;
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

    /// <summary>Entity mapping registry. If null, a new empty registry is used by the translator.</summary>
    public Mapping.IMappingRegistry MappingRegistry { get; set; } = new Mapping.MappingRegistry();
    
    /// <summary>Command timeout in seconds.</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>Explicitly set the entity type for mapping resolution. If null, use the generic type T from FlexQueryAsync.</summary>
    public Type? EntityType { get; set; }

    /// <summary>
    /// Configures the mapping for a specific entity type using fluent builder API.
    /// </summary>
    public Mapping.Builders.EntityTypeBuilder<TEntity> Entity<TEntity>() where TEntity : class
    {
        return MappingRegistry.Entity<TEntity>();
    }

    /// <summary>
    /// Scans the given assembly for types that match typical entity conventions
    /// (e.g., classes that aren't abstract, are public, and perhaps have key properties).
    /// </summary>
    public void ScanEntitiesFromAssembly(System.Reflection.Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic);

        foreach (var type in types)
        {
            // Only scan types that have an Id or Key property, or a Table attribute
            var hasKey = type.GetProperties().Any(p => 
                p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) || 
                p.Name.Equals(type.Name + "Id", StringComparison.OrdinalIgnoreCase) ||
                p.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.KeyAttribute), true).Any());
            
            var hasTable = type.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.Schema.TableAttribute), true).Any();

            if (hasKey || hasTable)
            {
                MappingRegistry.GetMapping(type);
            }
        }
    }
}
