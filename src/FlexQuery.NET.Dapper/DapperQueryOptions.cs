using FlexQuery.NET.Dapper.Configuration;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.Dapper;

/// <summary>
/// Represents Dapper-specific execution options for FlexQuery.NET.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DapperQueryOptions"/> extends <see cref="BaseQueryOptions"/> with
/// Dapper-specific settings such as the SQL dialect, command timeout, and runtime
/// mapping model.
/// </para>
/// <para>
/// These options can be created directly or initialized from an existing
/// <see cref="QueryExecutionOptions"/> instance.
/// </para>
/// </remarks>
public sealed class DapperQueryOptions : BaseQueryOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DapperQueryOptions"/> class
    /// with the default Dapper execution settings.
    /// </summary>
    public DapperQueryOptions()
    {
        IncludeTotalCount = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DapperQueryOptions"/> class
    /// by copying values from the specified query execution options.
    /// </summary>
    /// <param name="source">
    /// The base execution options to copy.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="source"/> is <see langword="null"/>.
    /// </exception>
    public DapperQueryOptions(QueryExecutionOptions source)
    {
        ArgumentNullException.ThrowIfNull(source);
        CopyBaseOptions(source, this);
    }

    internal FlexQueryModel? Model { get; private set; }

    /// <summary>
    /// Configures the runtime mapping model used for SQL generation.
    /// </summary>
    /// <param name="model">
    /// The mapping model produced by <see cref="ModelBuilder"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="model"/> is <see langword="null"/>.
    /// </exception>
    public void UseModel(FlexQueryModel model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
    }

    /// <summary>
    /// Gets or sets the SQL dialect used to generate database-specific SQL.
    /// </summary>
    /// <remarks>
    /// When not specified, the default dialect configured during application startup
    /// is used.
    /// </remarks>
    public ISqlDialect? Dialect { get; set; }

    /// <summary>
    /// Gets or sets the command timeout, in seconds, for Dapper commands.
    /// </summary>
    /// <value>
    /// The default value is <c>30</c> seconds.
    /// </value>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Creates a <see cref="QueryExecutionOptions"/> instance containing the
    /// shared execution settings.
    /// </summary>
    /// <returns>
    /// A new <see cref="QueryExecutionOptions"/> initialized from the current instance.
    /// </returns>
    public QueryExecutionOptions ToQueryExecutionOptions()
    {
        var target = new QueryExecutionOptions();
        CopyBaseOptions(this, target);
        return target;
    }

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
