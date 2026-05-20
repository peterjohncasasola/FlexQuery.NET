using FlexQuery.NET.Models;
using FlexQuery.NET.Dapper.Sql;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Security;
using System.Data.Common;

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
    /// <summary>Creates default Dapper query options.</summary>
    public DapperQueryOptions(QueryExecutionOptions source)
    {
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

    /// <summary>Converts to a base QueryExecutionOptions instance.</summary>
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

    /// <summary>Global default SQL dialect for all queries when not explicitly configured.</summary>
    public static ISqlDialect? GlobalDefaultDialect { get; set; }

    /// <summary>Global default resolver for SQL dialects.</summary>
    public static ISqlDialectResolver GlobalDialectResolver { get; set; } = new DefaultSqlDialectResolver();

    /// <summary>SQL dialect to use for query generation. If null, resolves via GlobalDefaultDialect or GlobalDialectResolver.</summary>
    public ISqlDialect? Dialect { get; set; }

    /// <summary>Entity mapping registry. If null, a new registry is created.</summary>
    public IMappingRegistry? MappingRegistry { get; set; }

    /// <summary>Command timeout in seconds.</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>Explicitly set the entity type for mapping resolution.</summary>
    public Type? EntityType { get; set; }

    /// <summary>
    /// Configures the mapping for a specific entity type using fluent builder API.
    /// </summary>
    public Mapping.Builders.EntityTypeBuilder<TEntity> Entity<TEntity>() where TEntity : class
    {
        return (MappingRegistry ?? new Mapping.MappingRegistry()).Entity<TEntity>();
    }

    /// <summary>
    /// Scans the given assembly for entity types.
    /// </summary>
    /// <summary>Scans the given assembly for entity types and registers them.</summary>
    public void ScanEntitiesFromAssembly(System.Reflection.Assembly assembly)
    {
        var registry = MappingRegistry ?? new Mapping.MappingRegistry();
        var types = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic);

        foreach (var type in types)
        {
            var hasKey = type.GetProperties().Any(p => 
                p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) || 
                p.Name.Equals(type.Name + "Id", StringComparison.OrdinalIgnoreCase) ||
                p.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.KeyAttribute), true).Any());
            
            var hasTable = type.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.Schema.TableAttribute), true).Any();

            if (hasKey || hasTable)
            {
                registry.GetMapping(type);
            }
        }

        if (MappingRegistry == null)
            MappingRegistry = registry;
    }

    /// <summary>
    /// Resolves the dialect by checking: explicit setting, GlobalDefaultDialect, GlobalDialectResolver.
    /// </summary>
    internal ISqlDialect ResolveDialect(DbConnection connection)
    {
        return Dialect 
            ?? GlobalDefaultDialect 
            ?? GlobalDialectResolver.Resolve(connection);
    }

    /// <summary>
    /// Resolves the mapping registry from explicit setting or creates a new one.
    /// </summary>
    internal IMappingRegistry ResolveMappingRegistry()
    {
        return MappingRegistry ?? new Mapping.MappingRegistry();
    }
}
