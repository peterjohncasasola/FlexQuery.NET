using System.Linq.Expressions;
using FlexQuery.NET.Models;

namespace FlexQuery.Benchmarks.Abstractions;

public interface IQueryParser<TParameters, TOptions>
{
    TOptions Parse(TParameters parameters);
}

public interface IExpressionBuilder<TOptions, TEntity>
{
    Expression<Func<TEntity, bool>> BuildFilter(TOptions options);
    IOrderedQueryable<TEntity> BuildSort(IQueryable<TEntity> query, TOptions options);
    IQueryable<object> BuildProjection(IQueryable<TEntity> query, TOptions options);
}

public interface IQueryExecutor<TEntity>
{
    Task<List<object>> ExecuteAsync(IQueryable<TEntity> query, CancellationToken ct = default);
}
