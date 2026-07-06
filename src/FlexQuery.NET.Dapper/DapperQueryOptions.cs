using FlexQuery.NET.Models;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Mapping.Builders;

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
    /// Copy constructor - creates a new instance by copying all properties from <paramref name="source"/>.
    /// </summary>
    public DapperQueryOptions(QueryExecutionOptions source)
    {
        ArgumentNullException.ThrowIfNull(source);
        CopyBaseOptions(source, this);
    }

    /// <summary>Converts to a base QueryExecutionOptions instance.</summary>
    public QueryExecutionOptions ToQueryExecutionOptions()
    {
        var target = new QueryExecutionOptions();
        CopyBaseOptions(this, target);
        return target;
    }
    
    /// <summary>SQL dialect to use for query generation. If null, resolves via SqlDialectResolver</summary>
    public ISqlDialect? Dialect { get; set; }

    internal MappingRegistry Registry { get; } = new();

    /// <summary>Command timeout in seconds.</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;
    
    public EntityTypeBuilder<TEntity> Entity<TEntity>() where TEntity : class => Registry.Entity<TEntity>();

    private static void CopyBaseOptions(BaseQueryOptions source, BaseQueryOptions target)
    {
        target.AllowedFields = source.AllowedFields;
        target.BlockedFields = source.BlockedFields;
        target.AllowedIncludes = source.AllowedIncludes;
        target.ExpressionMappings = source.ExpressionMappings;
        target.AllowedOperators = source.AllowedOperators;
        target.FilterableFields = source.FilterableFields;
        target.SortableFields = source.SortableFields;
        target.SelectableFields = source.SelectableFields;
        target.GroupableFields = source.GroupableFields;
        target.AggregatableFields = source.AggregatableFields;
        target.DefaultSortField = source.DefaultSortField;
        target.DefaultSortDescending = source.DefaultSortDescending;
        target.MaxFieldDepth = source.MaxFieldDepth;
        target.StrictFieldValidation = source.StrictFieldValidation;
        target.IncludeTotalCount = source.IncludeTotalCount;
        target.DefaultPageSize = source.DefaultPageSize;
        target.MaxPageSize = source.MaxPageSize;
        target.CaseInsensitiveFields = source.CaseInsensitiveFields;
        target.FieldMappings = source.FieldMappings;
        target.FieldAccessResolver = source.FieldAccessResolver;
        target.RoleAllowedFields = source.RoleAllowedFields;
        target.CurrentRole = source.CurrentRole;
        target.AllowedFieldsResolver = source.AllowedFieldsResolver;
    }
}
