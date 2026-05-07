using Microsoft.EntityFrameworkCore;
using FlexQuery.Benchmarks.Models;
using System.Collections.Generic;
using System.Linq;

namespace FlexQuery.Benchmarks.Abstractions;

/// <summary>
/// Base class for execution benchmarks that measure the time to generate AND execute a query.
/// </summary>
public abstract class ExecutionBenchmarkBase : BenchmarkBase
{
    /// <summary>
    /// Forces materialization of the query to avoid deferred execution artifacts.
    /// </summary>
    protected List<T> Materialize<T>(IQueryable<T> query)
    {
        return query.ToList();
    }

    /// <summary>
    /// Baseline implementation using pure LINQ.
    /// </summary>
    protected List<User> GetLinqBaseline(string status, int age)
    {
        return DbContext.Users
            .AsNoTracking()
            .Where(u => u.Status == status && u.Age > age)
            .OrderBy(u => u.Name)
            .Take(100)
            .ToList();
    }
}
