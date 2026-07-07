using FlexQuery.NET.Models;
using FlexQuery.NET.Dapper.Configuration;
using FlexQuery.NET.Dapper.Dialects;

namespace FlexQuery.NET.Dapper;

public sealed class DapperQueryOptions : BaseQueryOptions
{
    public DapperQueryOptions()
    {
        IncludeTotalCount = true;
    }

    public DapperQueryOptions(QueryExecutionOptions source)
    {
        ArgumentNullException.ThrowIfNull(source);
        CopyBaseOptions(source, this);
    }

    internal FlexQueryModel? Model { get; private set; }

    public void UseModel(FlexQueryModel model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public ISqlDialect? Dialect { get; set; }

    public int CommandTimeoutSeconds { get; set; } = 30;

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
