using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Options;
using DapperModelBuilder = FlexQuery.NET.Dapper.Configuration.ModelBuilder;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Tests.Integration;

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
                new Aggregate
                {
                    Field = "Total",
                    Function = AggregateFunction.Sum,
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
                new Aggregate
                {
                    Field = "Total",
                    Function = AggregateFunction.Sum,
                    Alias = "totalSum"
                }
            ],
            Having = new HavingConditionNode
            {
                Field = "Total",
                Function = AggregateFunction.Sum,
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
            new Order
            {
                Id = 100,
                Number = "RC-HAVING-100",
                CustomerId = 1,
                Total = 100m,
                OrderDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Order
            {
                Id = 101,
                Number = "RC-HAVING-101",
                CustomerId = 1,
                Total = 50m,
                OrderDate = new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc)
            },
            new Order
            {
                Id = 102,
                Number = "RC-HAVING-102",
                CustomerId = 2,
                Total = 70m,
                OrderDate = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc)
            },
            new Order
            {
                Id = 103,
                Number = "RC-HAVING-103",
                CustomerId = 2,
                Total = 50m,
                OrderDate = new DateTime(2026, 6, 4, 0, 0, 0, DateTimeKind.Utc)
            },
            new Order
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
    public async Task Dapper_IncludeWithTotalCount_ReturnsCorrectRootCount()
    {
        await using var db = SqlProjectionDbContext.CreateSeeded();
        var result = await ExecuteDapperCustomersAsync(db, new QueryOptions
        {
            IncludeCount = true,
            Paging = { Page = 1, PageSize = 10 }
        });

        result.TotalCount.Should().Be(10);
        result.Data.Should().HaveCount(10);
    }

    [Fact]
    public async Task Dapper_IncludeWithTotalCount_ReturnsCorrectRootCount_WithInclude()
    {
        await using var db = SqlProjectionDbContext.CreateSeeded();
        var result = await ExecuteDapperCustomersAsync(db, new QueryOptions
        {
            Includes = ["Orders"],
            IncludeCount = true,
            Paging = { Page = 1, PageSize = 10 }
        });

        result.TotalCount.Should().Be(10);
        result.Data.Should().NotBeEmpty();

        var alice = result.Data.OfType<Customer>().FirstOrDefault(c => c.Id == 1);
        alice.Should().NotBeNull();
        alice!.Orders.Should().HaveCount(2);
    }

    [Fact]
    public async Task Dapper_NavigationFilter_TotalCount_CountsRootEntities()
    {
        await using var db = SqlProjectionDbContext.CreateSeeded();
        var result = await ExecuteDapperCustomersAsync(db, new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters =
                [
                    new FilterCondition
                    {
                        Field = "Orders",
                        Operator = FilterOperators.Any,
                        ScopedFilter = new FilterGroup
                        {
                            Filters =
                            [
                                new FilterCondition
                                {
                                    Field = "Status",
                                    Operator = FilterOperators.Equal,
                                    Value = "Delivered"
                                }
                            ]
                        }
                    }
                ]
            },
            IncludeCount = true,
            Paging = { Page = 1, PageSize = 10 }
        });

        result.TotalCount.Should().Be(1);
        result.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task Dapper_IncludeTotalCountFalse_ReturnsNullTotalCount()
    {
        await using var db = SqlProjectionDbContext.CreateSeeded();
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
        await using var db = SqlProjectionDbContext.CreateSeeded();
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
            IncludeTotalCount = includeTotalCount
        };

        return await connection.FlexQueryAsync<Order>(
            options,
            options: CreateRegistry(dapperOptions));
    }

    private static async Task<QueryResult<object>> ExecuteDapperCustomersAsync(
        SqlProjectionDbContext db,
        QueryOptions options,
        bool includeTotalCount = true)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        var dapperOptions = new DapperQueryOptions
        {
            IncludeTotalCount = includeTotalCount
        };

        return await connection.FlexQueryAsync<Customer>(
            options,
            options: CreateRegistry(dapperOptions));
    }

    private static DapperQueryOptions CreateRegistry(DapperQueryOptions registry)
    {
        var flexQueryModel = SharedFlexQueryModel.Instance;
        registry.UseModel(flexQueryModel);
        return registry;
    }
}
