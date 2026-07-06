using System.Data.Common;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Extensions;
using FlexQuery.NET.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Tests.Tests;

public class ResultCountTests
{
    [Fact]
    public async Task EfCore_NormalQuery_SetsResultCountToTotalCount()
    {
        using var db = SqlProjectionDbContext.CreateSeeded();

        var result = await db.Orders.FlexQueryAsync(new QueryOptions
        {
            Paging = { Page = 1, PageSize = 2 },
            IncludeCount = true
        });

        result.TotalCount.Should().Be(4);
        result.ResultCount.Should().Be(4);
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task EfCore_GroupedQuery_SetsResultCountToGroupedRowsBeforePaging()
    {
        using var db = SqlProjectionDbContext.CreateSeeded();

        var result = await db.Orders.FlexQueryAsync(GroupedOptions(page: 1, pageSize: 2));

        result.TotalCount.Should().Be(4);
        result.ResultCount.Should().Be(3);
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task Dapper_NormalQuery_SetsResultCountToTotalCount()
    {
        await using var db = SqlProjectionDbContext.CreateSeeded();
        var result = await ExecuteDapperOrdersAsync(db, new QueryOptions
        {
            Paging = { Page = 1, PageSize = 2 },
            IncludeCount = true
        });

        result.TotalCount.Should().Be(4);
        result.ResultCount.Should().Be(4);
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task Dapper_GroupedQuery_SetsResultCountToGroupedRowsBeforePaging()
    {
        await using var db = SqlProjectionDbContext.CreateSeeded();
        var result = await ExecuteDapperOrdersAsync(db, GroupedOptions(page: 1, pageSize: 2));

        result.TotalCount.Should().Be(4);
        result.ResultCount.Should().Be(3);
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public void QueryResultConversions_PreserveResultCount()
    {
        var result = new QueryResult<string>
        {
            TotalCount = 10,
            ResultCount = 3,
            Page = 1,
            PageSize = 2,
            Data = ["one", "two"]
        };

        result.ToObjectResult().ResultCount.Should().Be(3);
        result.ToDynamicResult().ResultCount.Should().Be(3);
        result.ToProjectedQueryResult<string, object>().ResultCount.Should().Be(3);
    }

    [Fact]
    public async Task ResultCount_GroupedQuery_WithHaving_UsesPostHavingGroupCount()
    {
        await using var efDb = SqlProjectionDbContext.CreateSeeded();
        await AddHavingRowsAsync(efDb);

        var efResult = await efDb.Orders.FlexQueryAsync(GroupedHavingOptions(page: 1, pageSize: 1));

        efResult.TotalCount.Should().Be(5);
        efResult.ResultCount.Should().Be(2);
        efResult.Data.Should().HaveCount(1);

        using var dapperDb = SqlProjectionDbContext.CreateSeeded();
        await AddHavingRowsAsync(dapperDb);

        var dapperResult = await ExecuteDapperOrdersAsync(dapperDb, GroupedHavingOptions(page: 1, pageSize: 1));

        dapperResult.TotalCount.Should().Be(5);
        dapperResult.ResultCount.Should().Be(2);
        dapperResult.Data.Should().HaveCount(1);
    }

    private static QueryOptions GroupedOptions(int page, int pageSize)
    {
        return new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates =
            [
                new AggregateModel
                {
                    Field = "Total",
                    Function = "sum",
                    Alias = "totalSum"
                }
            ],
            Sort = [new SortNode { Field = "CustomerId" }],
            IncludeCount = true,
            Paging = { Page = page, PageSize = pageSize }
        };
    }

    private static QueryOptions GroupedHavingOptions(int page, int pageSize)
    {
        return new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates =
            [
                new AggregateModel
                {
                    Field = "Total",
                    Function = "sum",
                    Alias = "totalSum"
                }
            ],
            Having = new HavingCondition
            {
                Field = "Total",
                Function = "sum",
                Operator = FilterOperators.GreaterThan,
                Value = "100"
            },
            Filter = new FilterGroup
            {
                Filters =
                [
                    new FilterCondition
                    {
                        Field = "Number",
                        Operator = FilterOperators.StartsWith,
                        Value = "RC-HAVING-"
                    }
                ]
            },
            Sort = [new SortNode { Field = "CustomerId" }],
            IncludeCount = true,
            Paging = { Page = page, PageSize = pageSize }
        };
    }

    private static async Task AddHavingRowsAsync(SqlProjectionDbContext db)
    {
        db.Orders.AddRange(
            new SqlOrder
            {
                Id = 100,
                Number = "RC-HAVING-100",
                CustomerId = 1,
                Total = 100m,
                OrderDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new SqlOrder
            {
                Id = 101,
                Number = "RC-HAVING-101",
                CustomerId = 1,
                Total = 50m,
                OrderDate = new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc)
            },
            new SqlOrder
            {
                Id = 102,
                Number = "RC-HAVING-102",
                CustomerId = 2,
                Total = 70m,
                OrderDate = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc)
            },
            new SqlOrder
            {
                Id = 103,
                Number = "RC-HAVING-103",
                CustomerId = 2,
                Total = 50m,
                OrderDate = new DateTime(2026, 6, 4, 0, 0, 0, DateTimeKind.Utc)
            },
            new SqlOrder
            {
                Id = 104,
                Number = "RC-HAVING-104",
                CustomerId = 3,
                Total = 50m,
                OrderDate = new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc)
            });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Dapper_IncludeTotalCountFalse_ReturnsNullTotalCount()
    {
        using var db = SqlProjectionDbContext.CreateSeeded();
        var result = await ExecuteDapperOrdersAsync(db, new QueryOptions
        {
            Paging = { Page = 1, PageSize = 2 },
            IncludeCount = true
        }, includeTotalCount: false);

        result.TotalCount.Should().BeNull();
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task Dapper_IncludeTotalCountTrue_ReturnsActualCount()
    {
        using var db = SqlProjectionDbContext.CreateSeeded();
        var result = await ExecuteDapperOrdersAsync(db, new QueryOptions
        {
            Paging = { Page = 1, PageSize = 2 },
            IncludeCount = true
        }, includeTotalCount: true);

        result.TotalCount.Should().Be(4);
        result.Data.Should().HaveCount(2);
    }

    private static async Task<QueryResult<object>> ExecuteDapperOrdersAsync(
        SqlProjectionDbContext db,
        QueryOptions options,
        bool includeTotalCount = true)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        var dapperOptions = new DapperQueryOptions
        {
            Dialect = new SqliteDialect(),
            IncludeTotalCount = includeTotalCount
        };

        return await connection.FlexQueryAsync<SqlOrder>(
            options,
            dapperQueryOptions: CreateRegistry(dapperOptions));
    }

    private static DapperQueryOptions CreateRegistry(DapperQueryOptions registry)
    {
        registry.Entity<SqlCustomer>()
            .ToTable("Customers")
            .HasMany(c => c.Orders).WithForeignKey("CustomerId");
        registry.Entity<SqlOrder>()
            .ToTable("Orders")
            .HasMany(o => o.Items).WithForeignKey("OrderId");
        registry.Entity<SqlOrderItem>().ToTable("OrderItems");
        return registry;
    }
}
