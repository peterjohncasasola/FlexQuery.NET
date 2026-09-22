using System.Data.Common;
using Dapper;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Models;
using Microsoft.Extensions.Logging;

namespace FlexQuery.NET.Dapper.Context;

internal class DapperQueryContext
{
    public DbConnection Connection { get; init; } = null!;
    public QueryOptions QueryOptions { get; init; } = null!;
    public SqlTranslator? SqlTranslator { get; init; }
    public DynamicParameters? Parameters { get; init; }
    public DapperQueryOptions DapperQueryOptions { get; init; } = null!;
    public SqlCommand? SqlCommand { get; init; }
    public ILogger? SqlLogger { get; init; }
    public CancellationToken CancellationToken { get; init; }
}