using FlexQuery.NET.Builders;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared.Fixtures;
using FlexQuery.NET.Tests.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FlexQuery.NET.Tests.Validation;

/// <summary>
/// ApplyPaging must inject deterministic ordering whenever the query expression
/// carries no ordering — including the first page (Skip == 0). Providers in
/// split-query mode reject Skip/Take without ordering, and unordered pagination
/// is nondeterministic on every provider.
/// </summary>
public class QueryBuilderPagingTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SharedTestDbContext _db;

    public QueryBuilderPagingTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new SharedTestDbContext(new DbContextOptionsBuilder<SharedTestDbContext>()
            .UseSqlite(_connection)
            .Options);
        _db.Database.EnsureCreated();
        SampleData.Seed(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static QueryOptions PageOptions(int page, int pageSize) => new()
    {
        Paging = { Page = page, PageSize = pageSize }
    };

    [Fact]
    public void ApplyPaging_FirstPage_UnorderedQuery_InjectsOrdering()
    {
        var query = _db.Customers.Where(c => c.Id > 0);
        query.ToQueryString().Should().NotContain("ORDER BY", "precondition: query is unordered");

        var paged = QueryBuilder.ApplyPaging(query, PageOptions(1, 5));

        paged.ToQueryString().Should().Contain("ORDER BY");
        paged.ToQueryString().Should().NotContain("(SELECT 1)");
    }

    [Fact]
    public void ApplyPaging_SecondPage_UnorderedQuery_InjectsOrdering()
    {
        var query = _db.Customers.Where(c => c.Id > 0);

        var paged = QueryBuilder.ApplyPaging(query, PageOptions(2, 5));

        paged.ToQueryString().Should().Contain("ORDER BY");
        paged.ToQueryString().Should().NotContain("(SELECT 1)");
    }

    [Fact]
    public void ApplyPaging_AlreadyOrdered_KeepsClientOrdering()
    {
        var query = _db.Customers.Where(c => c.Id > 0).OrderBy(c => c.Name);

        var paged = QueryBuilder.ApplyPaging(query, PageOptions(1, 5));

        paged.ToQueryString().Should().Contain("Name");
        paged.ToQueryString().Should().NotContain("(SELECT 1)");
    }

    [Fact]
    public void ApplyPaging_Disabled_ReturnsUnchanged()
    {
        var options = PageOptions(1, 5);
        options.Paging.Disabled = true;

        var query = _db.Customers.Where(c => c.Id > 0);
        var paged = QueryBuilder.ApplyPaging(query, options);

        paged.ToQueryString().Should().NotContain("ORDER BY");
    }
}
