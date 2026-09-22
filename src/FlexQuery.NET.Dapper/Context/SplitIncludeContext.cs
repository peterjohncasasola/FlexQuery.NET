using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Models;

namespace FlexQuery.NET.Dapper.Context;

internal sealed class SplitIncludeContext : DapperQueryContext
{
    public IEntityMapping Mapping { get; init; } = null!;
    public IMappingRegistry Registry { get; init; } = null!;
    public ISqlDialect Dialect { get; init; } = null!;
    public SqlParameterContext ParameterContext { get; init; } = null!;
}