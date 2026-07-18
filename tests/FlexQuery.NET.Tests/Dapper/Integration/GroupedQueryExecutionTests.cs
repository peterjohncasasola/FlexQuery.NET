using System.Data.Common;
using System.Reflection;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Tests.Dapper.Integration;

public class GroupedQueryExecutionTests : IDisposable
{
    private readonly SqlProjectionDbContext _db = SqlProjectionDbContext.CreateSeeded();
    private readonly DbConnection _connection;

    public GroupedQueryExecutionTests()
    {
        _connection = _db.Database.GetDbConnection();
        if (_connection.State != System.Data.ConnectionState.Open)
            _connection.Open();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Dapper_GroupedAggregate_UsesAllFilteredRowsBeforePaging()
    {
        var additionalOrders = Enumerable.Range(0, 996)
            .Select(index => new Order
            {
                Id = 1000 + index,
                Number = $"GROUP-BULK-{index:D4}",
                CustomerId = (index % 3) + 1,
                Total = 1m,
                OrderDate = new DateTime(2026, 1, 1).AddMinutes(index)
            })
            .ToList();
        _db.Orders.AddRange(additionalOrders);
        await _db.SaveChangesAsync();

        var result = await ExecuteOrdersAsync(GroupedOptions(page: 1, pageSize: 10));

        result.Data.Should().HaveCount(3);
        var customerOne = result.Data.Single(row => Read<int>(row, "CustomerId") == 1);
        Read<double>(customerOne, "totalSum").Should().BeApproximately(507.0, 0.001);
    }

    [Fact]
    public async Task Dapper_GroupedPaging_PagesGroupedRowsWithoutRepeatingKeys()
    {
        var firstPageOptions = GroupedOptions(page: 1, pageSize: 2);
        firstPageOptions.Sort = [new SortNode { Field = "CustomerId" }];

        var secondPageOptions = GroupedOptions(page: 2, pageSize: 2);
        secondPageOptions.Sort = [new SortNode { Field = "CustomerId" }];

        var firstPage = await ExecuteOrdersAsync(firstPageOptions);
        var secondPage = await ExecuteOrdersAsync(secondPageOptions);

        var firstKeys = firstPage.Data.Select(row => Read<int>(row, "CustomerId")).ToList();
        var secondKeys = secondPage.Data.Select(row => Read<int>(row, "CustomerId")).ToList();

        firstKeys.Should().Equal(1, 2);
        secondKeys.Should().Equal(3);
        firstKeys.Should().NotIntersectWith(secondKeys);
    }

    [Fact]
    public async Task Dapper_MultiColumnGrouping_ProjectsAllGroupKeys()
    {
        await AddOrdersAsync(
            (100, 1, "2026-02-01", 10m),
            (101, 1, "2026-02-01", 15m),
            (102, 1, "2026-02-02", 20m),
            (103, 2, "2026-02-01", 30m));

        var result = await ExecuteOrdersAsync(MultiColumnGroupedOptions(page: 1, pageSize: 10));

        var keys = result.Data.Select(CompositeKey).ToList();
        keys.Should().Contain(["1|2026-02-01", "1|2026-02-02", "2|2026-02-01"]);
        Read<double>(result.Data.Single(row => CompositeKey(row) == "1|2026-02-01"), "totalSum")
            .Should().BeApproximately(25, 0.001);
    }

    [Fact]
    public async Task Dapper_GroupedHaving_AppliesBeforePaging()
    {
        await AddOrdersWithNumberPrefixAsync(
            "HAVING",
            (100, 1, "2026-03-01", 100m),
            (101, 1, "2026-03-02", 50m),
            (102, 2, "2026-03-01", 90m),
            (103, 2, "2026-03-02", 20m),
            (104, 3, "2026-03-01", 200m));

        var firstPageOptions = CountGroupedOptionsWithHaving(page: 1, pageSize: 1, minimumCount: 1);
        firstPageOptions.Filter = NumberPrefixFilter("HAVING-");
        firstPageOptions.Sort = [new SortNode { Field = "CustomerId" }];

        var secondPageOptions = CountGroupedOptionsWithHaving(page: 2, pageSize: 1, minimumCount: 1);
        secondPageOptions.Filter = NumberPrefixFilter("HAVING-");
        secondPageOptions.Sort = [new SortNode { Field = "CustomerId" }];

        var thirdPageOptions = CountGroupedOptionsWithHaving(page: 3, pageSize: 1, minimumCount: 1);
        thirdPageOptions.Filter = NumberPrefixFilter("HAVING-");
        thirdPageOptions.Sort = [new SortNode { Field = "CustomerId" }];

        var firstPage = await ExecuteOrdersAsync(firstPageOptions);
        var secondPage = await ExecuteOrdersAsync(secondPageOptions);
        var thirdPage = await ExecuteOrdersAsync(thirdPageOptions);

        firstPage.Data.Select(row => Read<int>(row, "CustomerId")).Should().Equal(1);
        secondPage.Data.Select(row => Read<int>(row, "CustomerId")).Should().Equal(2);
        thirdPage.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task Dapper_GroupOnly_WorksWithoutAggregates()
    {
        var firstPage = await ExecuteOrdersAsync(new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Sort = [new SortNode { Field = "CustomerId", Descending = true }],
            Paging = { Page = 1, PageSize = 2 }
        });
        var secondPage = await ExecuteOrdersAsync(new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Sort = [new SortNode { Field = "CustomerId", Descending = true }],
            Paging = { Page = 2, PageSize = 2 }
        });

        firstPage.Data.Select(row => Read<int>(row, "CustomerId")).Should().Equal(3, 2);
        secondPage.Data.Select(row => Read<int>(row, "CustomerId")).Should().Equal(1);
        firstPage.Data.Concat(secondPage.Data)
            .Select(row => ToDictionary(row).Keys)
            .Should().OnlyContain(keys => keys.SequenceEqual(new[] { "CustomerId" }));
    }

    [Fact]
    public async Task Dapper_GroupedCountAggregate_ProjectsCorrectCounts()
    {
        await AddOrdersWithNumberPrefixAsync(
            "COUNT",
            (100, 1, "2026-04-01", 10m),
            (101, 1, "2026-04-02", 15m),
            (102, 2, "2026-04-01", 20m));

        var options = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates =
            [
                new Aggregate { Field = "Id", Function = AggregateFunction.Count, Alias = "idCount" },
                new Aggregate { Field = "Total", Function = AggregateFunction.Sum, Alias = "totalSum" }
            ],
            Filter = NumberPrefixFilter("COUNT-"),
            Sort = [new SortNode { Field = "CustomerId" }],
            Paging = { Page = 1, PageSize = 10 }
        };

        var result = await ExecuteOrdersAsync(options);

        result.Data.Select(row => Read<int>(row, "CustomerId")).Should().Equal(1, 2);
        result.Data.Select(row => Read<int>(row, "idCount")).Should().Equal(2, 1);
        result.Data.Select(row => Read<double>(row, "totalSum")).Should().Equal(25, 20);
    }

    private Task<QueryResult<object>> ExecuteOrdersAsync(QueryOptions options)
    {
        var dapperOptions = new DapperQueryOptions
        {
            IncludeTotalCount = true
        };
        dapperOptions.UseModel(SharedFlexQueryModel.Instance);
        
        return _connection.FlexQueryAsync<Order>(options, dapperOptions);
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
            Paging = { Page = page, PageSize = pageSize }
        };
    }

    private static QueryOptions MultiColumnGroupedOptions(int page, int pageSize)
    {
        return new QueryOptions
        {
            GroupBy = ["CustomerId", "OrderDate"],
            Aggregates =
            [
                new Aggregate
                {
                    Field = "Total",
                    Function = AggregateFunction.Sum,
                    Alias = "totalSum"
                }
            ],
            Paging = { Page = page, PageSize = pageSize }
        };
    }

    private static QueryOptions GroupedOptionsWithHaving(int page, int pageSize, decimal minimumTotal)
    {
        var options = GroupedOptions(page, pageSize);
        options.Having = new HavingConditionNode
        {
            Field = "Total",
            Function = AggregateFunction.Sum,
            Operator = FilterOperators.GreaterThan,
            Value = minimumTotal.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        return options;
    }

    private static QueryOptions CountGroupedOptionsWithHaving(int page, int pageSize, int minimumCount)
    {
        return new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates =
            [
                new Aggregate
                {
                    Field = "Id",
                    Function = AggregateFunction.Count,
                    Alias = "idCount"
                }
            ],
            Having = new HavingConditionNode
            {
                Field = "Id",
                Function = AggregateFunction.Count,
                Operator = FilterOperators.GreaterThan,
                Value = minimumCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            },
            Paging = { Page = page, PageSize = pageSize }
        };
    }

    private static FilterGroup NumberPrefixFilter(string prefix)
    {
        return new FilterGroup
        {
            Filters =
            [
                new FilterCondition
                {
                    Field = "Number",
                    Operator = FilterOperators.StartsWith,
                    Value = prefix
                }
            ]
        };
    }

    private async Task AddOrdersAsync(params (int Id, int CustomerId, string Date, decimal Total)[] rows)
    {
        await AddOrdersWithNumberPrefixAsync("GROUP", rows);
    }

    private async Task AddOrdersWithNumberPrefixAsync(
        string prefix,
        params (int Id, int CustomerId, string Date, decimal Total)[] rows)
    {
        var orders = rows
            .Select(row => new Order
            {
                Id = row.Id,
                Number = $"{prefix}-{row.Id}",
                CustomerId = row.CustomerId,
                Total = row.Total,
                OrderDate = DateTime.SpecifyKind(DateTime.Parse(row.Date), DateTimeKind.Utc)
            })
            .ToList();

        _db.Orders.AddRange(orders);
        await _db.SaveChangesAsync();
    }

    private static string CompositeKey(object row)
        => $"{Read<int>(row, "CustomerId")}|{Read<DateTime>(row, "OrderDate"):yyyy-MM-dd}";

    private static Dictionary<string, object?> ToDictionary(object row)
    {
        if (row is Dictionary<string, object?> dict)
            return dict;
        if (row is IReadOnlyDictionary<string, object?> readOnlyDict)
            return new Dictionary<string, object?>(readOnlyDict, StringComparer.OrdinalIgnoreCase);

        var props = row.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in props)
            result[prop.Name] = prop.GetValue(row);
        return result;
    }

    private static T Read<T>(object row, string propertyName)
    {
        if (row is IReadOnlyDictionary<string, object?> dictionary)
        {
            dictionary.TryGetValue(propertyName, out var value).Should().BeTrue(
                $"the Dapper row should contain {propertyName}; available keys are {string.Join(", ", dictionary.Keys)}");

            if (value is null)
                return default!;

            return (T)Convert.ChangeType(value, typeof(T))!;
        }

        var prop = row.GetType().GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        prop.Should().NotBeNull($"the Dapper row should have a property named {propertyName}");
        var propValue = prop!.GetValue(row);
        if (propValue is null)
            return default!;

        return (T)Convert.ChangeType(propValue, typeof(T))!;
    }

}
