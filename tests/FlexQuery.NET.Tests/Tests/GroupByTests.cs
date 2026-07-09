using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Parsers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace FlexQuery.NET.Tests.Tests;

public class GroupByTests : IDisposable
{
    private readonly SqlProjectionDbContext _db = SqlProjectionDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    private static QueryOptions Parse(Dictionary<string, string> raw)
    {
        var kvps = raw.Select(kv =>
            new KeyValuePair<string, StringValues>(kv.Key, new StringValues(kv.Value)));
        return QueryOptionsParser.Parse(kvps);
    }

    [Fact]
    public async Task GroupBy_SelectAggregates_Having_AppliesServerTranslatableShape()
    {
        var options = Parse(new()
        {
            ["group"] = "CustomerId",
            ["select"] = "CustomerId",
            ["aggregates"] = "Total:sum,Id:count",
            ["having"] = "sum(Total):gt:100"
        });

        var query = _db.Orders.AsQueryable().ApplySelect(options);
        var sql = query.ToQueryString();
        sql.ToUpperInvariant().Should().Contain("GROUP BY");
        sql.ToUpperInvariant().Should().Contain("HAVING");

        var rows = await query.ToListAsync();
        rows.Should().ContainSingle();
    }

    [Fact]
    public async Task GroupBy_LinqStyleAggregates_Works()
    {
        var options = Parse(new()
        {
            ["group"] = "CustomerId",
            ["select"] = "CustomerId",
            ["aggregates"] = "Total:sum,Id:count",
        });

        options.Aggregates.Should().HaveCount(2, "it should have parsed two aggregates from the aggregates parameter");
        options.Aggregates[0].Function.Should().Be(AggregateFunction.Sum);
        options.Aggregates[0].Field.Should().Be("Total");
        options.Aggregates[0].Alias.Should().Be("TotalSum");
        options.Aggregates[1].Function.Should().Be(AggregateFunction.Count);
        options.Aggregates[1].Field.Should().Be("Id");
        options.Aggregates[1].Alias.Should().Be("IdCount");

        var query = _db.Orders.AsQueryable().ApplySelect(options);
        var rows = await query.ToListAsync();
        rows.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedAggregate_UsesAllFilteredRowsBeforePaging()
    {
        var additionalOrders = Enumerable.Range(0, 996)
            .Select(index => new SqlOrder
            {
                Id = 1000 + index,
                Number = $"BULK-{index:D4}",
                CustomerId = (index % 3) + 1,
                Total = 1m,
                OrderDate = new DateTime(2026, 1, 1).AddMinutes(index)
            })
            .ToList();
        _db.Orders.AddRange(additionalOrders);
        await _db.SaveChangesAsync();

        var options = GroupedOptions(page: 1, pageSize: 10);
        var result = await _db.Orders.FlexQueryAsync(options);

        result.Data.Should().HaveCount(3);
        Read<int>(result.Data[0], "CustomerId").Should().Be(1);
        Read<double>(result.Data[0], "totalSum").Should().BeApproximately(502.5, 0.001);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedPaging_PagesGroupsWithoutRepeatingKeys()
    {
        var firstPage = await _db.Orders.FlexQueryAsync(GroupedOptions(page: 1, pageSize: 2));
        var secondPage = await _db.Orders.FlexQueryAsync(GroupedOptions(page: 2, pageSize: 2));

        var firstKeys = firstPage.Data.Select(row => Read<int>(row, "CustomerId")).ToList();
        var secondKeys = secondPage.Data.Select(row => Read<int>(row, "CustomerId")).ToList();

        firstKeys.Should().Equal(1, 2);
        secondKeys.Should().Equal(3);
        firstKeys.Should().NotIntersectWith(secondKeys);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedSort_ByGroupField_AppliesAfterGrouping()
    {
        var options = GroupedOptions(page: 1, pageSize: 3);
        options.Sort = [new SortNode { Field = "CustomerId", Descending = true }];

        var result = await _db.Orders.FlexQueryAsync(options);

        result.Data.Select(row => Read<int>(row, "CustomerId")).Should().Equal(3, 2, 1);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedSort_ByAggregateAlias_AppliesAfterGrouping()
    {
        var options = GroupedOptions(page: 1, pageSize: 3);
        options.Sort = [new SortNode { Field = "totalSum", Descending = true }];

        var result = await _db.Orders.FlexQueryAsync(options);

        result.Data.Select(row => Read<int>(row, "CustomerId")).Should().Equal(1, 2, 3);
        result.Data.Select(row => Read<double>(row, "totalSum")).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task FlexQueryAsync_MultiColumnGrouping_ProjectsAllGroupKeysAndPagesGroupedRows()
    {
        await AddOrdersAsync(
            (100, 1, "2026-02-01", 10m),
            (101, 1, "2026-02-01", 15m),
            (102, 1, "2026-02-02", 20m),
            (103, 2, "2026-02-01", 30m),
            (104, 2, "2026-02-01", 35m),
            (105, 2, "2026-02-02", 40m),
            (106, 3, "2026-02-01", 50m));

        var firstPage = await _db.Orders.FlexQueryAsync(MultiColumnGroupedOptions(page: 1, pageSize: 3));
        var secondPage = await _db.Orders.FlexQueryAsync(MultiColumnGroupedOptions(page: 2, pageSize: 3));

        var firstKeys = firstPage.Data.Select(CompositeKey).ToList();
        var secondKeys = secondPage.Data.Select(CompositeKey).ToList();

        firstPage.Data.Should().OnlyContain(row =>
            Read<int>(row, "CustomerId") > 0
            && Read<DateTime>(row, "OrderDate") != default);
        firstKeys.Should().Equal(
            "1|2025-01-01",
            "1|2025-01-02",
            "1|2026-02-01");
        secondKeys.Should().Equal(
            "1|2026-02-02",
            "2|2025-01-03",
            "2|2026-02-01");
        firstKeys.Should().NotIntersectWith(secondKeys);
        Read<double>(firstPage.Data[2], "totalSum").Should().BeApproximately(25, 0.001);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedHaving_AppliesBeforePaging()
    {
        await AddOrdersAsync(
            (100, 1, "2026-03-01", 400m),
            (101, 1, "2026-03-02", 10m),
            (102, 2, "2026-03-01", 200m),
            (103, 2, "2026-03-02", 10m),
            (104, 3, "2026-03-01", 20m));

        var firstPage = await _db.Orders.FlexQueryAsync(GroupedOptionsWithHaving(page: 1, pageSize: 1, minimumTotal: 250));
        var secondPage = await _db.Orders.FlexQueryAsync(GroupedOptionsWithHaving(page: 2, pageSize: 1, minimumTotal: 250));
        var thirdPage = await _db.Orders.FlexQueryAsync(GroupedOptionsWithHaving(page: 3, pageSize: 1, minimumTotal: 250));

        firstPage.Data.Select(row => Read<int>(row, "CustomerId")).Should().Equal(1);
        secondPage.Data.Select(row => Read<int>(row, "CustomerId")).Should().Equal(2);
        thirdPage.Data.Should().BeEmpty();
        Read<double>(firstPage.Data[0], "totalSum").Should().BeApproximately(580.5, 0.001);
        Read<double>(secondPage.Data[0], "totalSum").Should().BeApproximately(309, 0.001);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedPaging_UsesDeterministicGroupKeyOrderingWhenNoSortExists()
    {
        await AddOrdersAsync(
            (100, 2, "2026-04-02", 1m),
            (101, 1, "2026-04-03", 1m),
            (102, 1, "2026-04-01", 1m),
            (103, 2, "2026-04-01", 1m));

        var firstRequest = await _db.Orders.FlexQueryAsync(MultiColumnGroupedOptions(page: 1, pageSize: 4));
        var repeatedFirstRequest = await _db.Orders.FlexQueryAsync(MultiColumnGroupedOptions(page: 1, pageSize: 4));
        var secondRequest = await _db.Orders.FlexQueryAsync(MultiColumnGroupedOptions(page: 2, pageSize: 4));

        var firstKeys = firstRequest.Data.Select(CompositeKey).ToList();
        var repeatedFirstKeys = repeatedFirstRequest.Data.Select(CompositeKey).ToList();
        var secondKeys = secondRequest.Data.Select(CompositeKey).ToList();

        firstKeys.Should().Equal(
            "1|2025-01-01",
            "1|2025-01-02",
            "1|2026-04-01",
            "1|2026-04-03");
        repeatedFirstKeys.Should().Equal(firstKeys);
        secondKeys.Should().StartWith(
            "2|2025-01-03",
            "2|2026-04-01",
            "2|2026-04-02");
        firstKeys.Should().NotIntersectWith(secondKeys);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedQuery_WithNoMatchingRows_ReturnsEmptyDataAndZeroTotalCount()
    {
        var options = GroupedOptions(page: 1, pageSize: 10);
        options.IncludeCount = true;
        options.Filter = new FilterGroup
        {
            Filters =
            [
                new FilterCondition
                {
                    Field = "Number",
                    Operator = FilterOperators.Equal,
                    Value = "NO-SUCH-ORDER"
                }
            ]
        };

        var result = await _db.Orders.FlexQueryAsync(options);

        result.Data.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedQuery_PreservesFilteredSourceTotalCount()
    {
        await AddOrdersAsync(
            (100, 1, "2026-05-01", 1m),
            (101, 1, "2026-05-02", 1m),
            (102, 2, "2026-05-01", 1m),
            (103, 2, "2026-05-02", 1m),
            (104, 3, "2026-05-01", 1m));

        var options = GroupedOptions(page: 1, pageSize: 2);
        options.IncludeCount = true;
        options.Filter = new FilterGroup
        {
            Filters =
            [
                new FilterCondition
                {
                    Field = "Number",
                    Operator = FilterOperators.StartsWith,
                    Value = "GROUP-"
                }
            ]
        };

        var result = await _db.Orders.FlexQueryAsync(options);

        result.TotalCount.Should().Be(5);
        result.Data.Should().HaveCount(2);
        result.Data.Select(row => Read<int>(row, "CustomerId")).Should().Equal(1, 2);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedMultiSort_AppliesOrderByAndThenBy()
    {
        await AddOrdersAsync(
            (100, 1, "2026-06-01", 10m),
            (101, 1, "2026-06-02", 40m),
            (102, 1, "2026-06-03", 25m),
            (103, 2, "2026-06-01", 5m),
            (104, 2, "2026-06-02", 35m));

        var options = MultiColumnGroupedOptions(page: 1, pageSize: 5);
        options.Filter = GroupOrderFilter();
        options.Sort =
        [
            new SortNode { Field = "CustomerId" },
            new SortNode { Field = "totalSum", Descending = true }
        ];

        var result = await _db.Orders.FlexQueryAsync(options);

        result.Data.Select(CompositeKey).Should().Equal(
            "1|2026-06-02",
            "1|2026-06-03",
            "1|2026-06-01",
            "2|2026-06-02",
            "2|2026-06-01");
        result.Data.Select(row => Read<double>(row, "totalSum")).Should().Equal(40, 25, 10, 35, 5);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedNullKeys_ReturnsNullGroupWithCorrectAggregates()
    {
        await using var db = TestDbContext.CreateSeeded();
        db.Entities.AddRange(
            new TestEntity
            {
                Id = 100,
                Name = "Null Profile One",
                Age = 10,
                City = "Null City",
                CreatedAt = new DateTime(2026, 7, 1),
                Status = Status.Active,
                Profile = null
            },
            new TestEntity
            {
                Id = 101,
                Name = "Null Profile Two",
                Age = 20,
                City = "Null City",
                CreatedAt = new DateTime(2026, 7, 2),
                Status = Status.Active,
                Profile = null
            });
        await db.SaveChangesAsync();

        var options = new QueryOptions
        {
            GroupBy = ["Profile.Bio"],
            Aggregates =
            [
                new AggregateModel { Field = "Age", Function = AggregateFunction.Sum, Alias = "ageSum" },
                new AggregateModel { Field = "Id", Function = AggregateFunction.Count, Alias = "idCount" }
            ],
            Filter = new FilterGroup
            {
                Filters =
                [
                    new FilterCondition
                    {
                        Field = "City",
                        Operator = FilterOperators.Equal,
                        Value = "Null City"
                    }
                ]
            },
            Paging = { Page = 1, PageSize = 10 }
        };

        var result = await db.Entities.FlexQueryAsync(options);

        result.Data.Should().ContainSingle();
        Read<string?>(result.Data[0], "Bio").Should().BeNull();
        Read<double>(result.Data[0], "ageSum").Should().BeApproximately(30, 0.001);
        Read<int>(result.Data[0], "idCount").Should().Be(2);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedMultipleAggregates_ProjectsAllAliasesWithCorrectValues()
    {
        await AddOrdersAsync(
            (100, 1, "2026-08-01", 10m),
            (101, 1, "2026-08-02", 20m),
            (102, 1, "2026-08-03", 30m),
            (103, 2, "2026-08-01", 5m));

        var options = MultiAggregateGroupedOptions(page: 1, pageSize: 10);
        options.Filter = GroupOrderFilter();
        options.Sort = [new SortNode { Field = "CustomerId" }];

        var result = await _db.Orders.FlexQueryAsync(options);

        result.Data.Should().HaveCount(2);

        var customerOne = result.Data.Single(row => Read<int>(row, "CustomerId") == 1);
        Read<double>(customerOne, "totalSum").Should().BeApproximately(60, 0.001);
        Read<double>(customerOne, "totalAvg").Should().BeApproximately(20, 0.001);
        Read<double>(customerOne, "totalMin").Should().BeApproximately(10, 0.001);
        Read<double>(customerOne, "totalMax").Should().BeApproximately(30, 0.001);
        Read<int>(customerOne, "idCount").Should().Be(3);

        var aliases = customerOne.GetType().GetProperties().Select(property => property.Name).ToList();
        aliases.Should().Contain(["totalSum", "totalAvg", "totalMin", "totalMax", "idCount"]);
        aliases.Distinct(StringComparer.OrdinalIgnoreCase).Should().HaveSameCount(aliases);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedHavingWithAggregateSort_FiltersBeforeSortingAndPaging()
    {
        await AddOrdersAsync(
            (100, 1, "2026-09-01", 100m),
            (101, 1, "2026-09-02", 50m),
            (102, 2, "2026-09-01", 300m),
            (103, 3, "2026-09-01", 120m));

        var options = GroupedOptionsWithHaving(page: 1, pageSize: 2, minimumTotal: 100);
        options.Filter = GroupOrderFilter();
        options.Sort = [new SortNode { Field = "totalSum", Descending = true }];

        var result = await _db.Orders.FlexQueryAsync(options);

        result.Data.Select(row => Read<int>(row, "CustomerId")).Should().Equal(2, 1);
        result.Data.Select(row => Read<double>(row, "totalSum")).Should().Equal(300, 150);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedAggregateSortWithPaging_ReturnsDistinctStablePages()
    {
        await AddOrdersAsync(
            (100, 1, "2026-10-01", 100m),
            (101, 2, "2026-10-01", 300m),
            (102, 3, "2026-10-01", 200m));

        var firstPageOptions = GroupedOptions(page: 1, pageSize: 2);
        firstPageOptions.Filter = GroupOrderFilter();
        firstPageOptions.Sort = [new SortNode { Field = "totalSum", Descending = true }];

        var secondPageOptions = GroupedOptions(page: 2, pageSize: 2);
        secondPageOptions.Filter = GroupOrderFilter();
        secondPageOptions.Sort = [new SortNode { Field = "totalSum", Descending = true }];

        var firstPage = await _db.Orders.FlexQueryAsync(firstPageOptions);
        var secondPage = await _db.Orders.FlexQueryAsync(secondPageOptions);

        var firstKeys = firstPage.Data.Select(row => Read<int>(row, "CustomerId")).ToList();
        var secondKeys = secondPage.Data.Select(row => Read<int>(row, "CustomerId")).ToList();

        firstKeys.Should().Equal(2, 3);
        secondKeys.Should().Equal(1);
        firstKeys.Should().NotIntersectWith(secondKeys);
        firstPage.Data.Concat(secondPage.Data)
            .Select(row => Read<double>(row, "totalSum"))
            .Should().Equal(300, 200, 100);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedCountAggregate_ProjectsCorrectCountsAlongsideOtherAggregates()
    {
        await AddOrdersAsync(
            (100, 1, "2026-11-01", 10m),
            (101, 1, "2026-11-02", 15m),
            (102, 2, "2026-11-01", 20m));

        var options = MultiAggregateGroupedOptions(page: 1, pageSize: 10);
        options.Filter = GroupOrderFilter();
        options.Sort = [new SortNode { Field = "idCount", Descending = true }];

        var result = await _db.Orders.FlexQueryAsync(options);

        result.Data.Select(row => Read<int>(row, "CustomerId")).Should().Equal(1, 2);
        result.Data.Select(row => Read<int>(row, "idCount")).Should().Equal(2, 1);
        result.Data.Select(row => Read<double>(row, "totalSum")).Should().Equal(25, 20);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedQuery_FullPipeline_AppliesOperationsInCorrectOrder()
    {
        await AddOrdersWithNumberPrefixAsync(
            "PIPE",
            (100, 1, "2026-12-01", 100m),
            (101, 1, "2026-12-02", 50m),
            (102, 2, "2026-12-01", 90m),
            (103, 2, "2026-12-02", 20m),
            (104, 3, "2026-12-01", 200m),
            (105, 3, "2026-12-02", 5m));
        await AddOrdersWithNumberPrefixAsync(
            "DROP",
            (106, 1, "2026-12-03", 1000m),
            (107, 2, "2026-12-03", 1000m));

        var options = GroupedOptionsWithHaving(page: 2, pageSize: 1, minimumTotal: 120);
        options.Filter = NumberPrefixFilter("PIPE-");
        options.Sort = [new SortNode { Field = "totalSum", Descending = true }];

        var result = await _db.Orders.FlexQueryAsync(options);

        result.Data.Should().ContainSingle();
        Read<int>(result.Data[0], "CustomerId").Should().Be(1);
        Read<double>(result.Data[0], "totalSum").Should().BeApproximately(150, 0.001);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupOnly_WorksWithoutAggregates()
    {
        var firstPageOptions = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Sort = [new SortNode { Field = "CustomerId", Descending = true }],
            Paging = { Page = 1, PageSize = 2 }
        };
        var secondPageOptions = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Sort = [new SortNode { Field = "CustomerId", Descending = true }],
            Paging = { Page = 2, PageSize = 2 }
        };

        var firstPage = await _db.Orders.FlexQueryAsync(firstPageOptions);
        var secondPage = await _db.Orders.FlexQueryAsync(secondPageOptions);

        firstPage.Data.Select(row => Read<int>(row, "CustomerId")).Should().Equal(3, 2);
        secondPage.Data.Select(row => Read<int>(row, "CustomerId")).Should().Equal(1);
        firstPage.Data.Concat(secondPage.Data)
            .Select(row => row.GetType().GetProperties().Select(property => property.Name))
            .Should().OnlyContain(properties => properties.SequenceEqual(new[] { "CustomerId" }));
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedHaving_Count_FiltersByCount()
    {
        var options = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates = [new AggregateModel { Function = AggregateFunction.Count, Alias = "Count" }],
            Having = new HavingCondition
            {
                Function = AggregateFunction.Count,
                Operator = FilterOperators.GreaterThan,
                Value = "1"
            },
            Paging = { Page = 1, PageSize = 10 }
        };

        var result = await _db.Orders.FlexQueryAsync(options);
        result.Data.Should().ContainSingle();
        Read<int>(result.Data[0], "CustomerId").Should().Be(1);
        Read<int>(result.Data[0], "Count").Should().Be(2);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedHaving_Sum_FiltersBySum()
    {
        var options = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates = [new AggregateModel { Field = "Total", Function = AggregateFunction.Sum, Alias = "totalSum" }],
            Having = new HavingCondition
            {
                Field = "Total",
                Function = AggregateFunction.Sum,
                Operator = FilterOperators.GreaterThan,
                Value = "100"
            },
            Paging = { Page = 1, PageSize = 10 }
        };

        var result = await _db.Orders.FlexQueryAsync(options);
        result.Data.Should().ContainSingle();
        Read<int>(result.Data[0], "CustomerId").Should().Be(1);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedHaving_Avg_FiltersByAverage()
    {
        var options = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates = [new AggregateModel { Field = "Total", Function = AggregateFunction.Avg, Alias = "totalAvg" }],
            Having = new HavingCondition
            {
                Field = "Total",
                Function = AggregateFunction.Avg,
                Operator = FilterOperators.GreaterThan,
                Value = "50"
            },
            Paging = { Page = 1, PageSize = 10 }
        };

        var result = await _db.Orders.FlexQueryAsync(options);
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedHaving_Max_FiltersByMax()
    {
        var options = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates = [new AggregateModel { Field = "Total", Function = AggregateFunction.Max, Alias = "totalMax" }],
            Having = new HavingCondition
            {
                Field = "Total",
                Function = AggregateFunction.Max,
                Operator = FilterOperators.GreaterThan,
                Value = "100"
            },
            Paging = { Page = 1, PageSize = 10 }
        };

        var result = await _db.Orders.FlexQueryAsync(options);
        result.Data.Should().ContainSingle();
        Read<int>(result.Data[0], "CustomerId").Should().Be(1);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedHaving_Min_FiltersByMin()
    {
        var options = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates = [new AggregateModel { Field = "Total", Function = AggregateFunction.Min, Alias = "totalMin" }],
            Having = new HavingCondition
            {
                Field = "Total",
                Function = AggregateFunction.Min,
                Operator = FilterOperators.GreaterThan,
                Value = "45"
            },
            Paging = { Page = 1, PageSize = 10 }
        };

        var result = await _db.Orders.FlexQueryAsync(options);
        result.Data.Should().ContainSingle();
        Read<int>(result.Data[0], "CustomerId").Should().Be(2);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedHaving_CountWithSort_SortsAfterHaving()
    {
        await AddOrdersAsync(
            (201, 2, "2026-05-01", 5m));

        var options = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates = [new AggregateModel { Function = AggregateFunction.Count, Alias = "Count" }],
            Having = new HavingCondition
            {
                Function = AggregateFunction.Count,
                Operator = FilterOperators.GreaterThanOrEq,
                Value = "2"
            },
            Sort = [new SortNode { Field = "CustomerId", Descending = true }],
            Paging = { Page = 1, PageSize = 10 }
        };

        var result = await _db.Orders.FlexQueryAsync(options);
        result.Data.Should().HaveCount(2);
        Read<int>(result.Data[0], "CustomerId").Should().Be(2);
        Read<int>(result.Data[1], "CustomerId").Should().Be(1);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedHaving_WithPaging_PagesAfterHaving()
    {
        await AddOrdersAsync(
            (300, 1, "2026-06-01", 5m));

        var options = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates = [new AggregateModel { Function = AggregateFunction.Count, Alias = "Count" }],
            Having = new HavingCondition
            {
                Function = AggregateFunction.Count,
                Operator = FilterOperators.GreaterThanOrEq,
                Value = "1"
            },
            Sort = [new SortNode { Field = "CustomerId" }],
            Paging = { Page = 1, PageSize = 2 }
        };

        var result = await _db.Orders.FlexQueryAsync(options);
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedHaving_WithProjection_SelectsOnlyProjectedFields()
    {
        var options = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates = [new AggregateModel { Function = AggregateFunction.Count, Alias = "Count" }],
            Having = new HavingCondition
            {
                Function = AggregateFunction.Count,
                Operator = FilterOperators.GreaterThan,
                Value = "1"
            },
            Paging = { Page = 1, PageSize = 10 }
        };

        var result = await _db.Orders.FlexQueryAsync(options);
        result.Data.Should().ContainSingle();

        var row = result.Data[0];
        Read<int>(row, "CustomerId").Should().Be(1);
        Read<int>(row, "Count").Should().Be(2);
    }

    [Fact]
    public async Task FlexQueryAsync_GroupedHaving_HavingFiltersOutAllRows_ReturnsEmpty()
    {
        var options = new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates = [new AggregateModel { Function = AggregateFunction.Count, Alias = "Count" }],
            Having = new HavingCondition
            {
                Function = AggregateFunction.Count,
                Operator = FilterOperators.GreaterThan,
                Value = "999"
            },
            Sort = [new SortNode { Field = "CustomerId" }],
            Paging = { Page = 1, PageSize = 10 }
        };

        var result = await _db.Orders.FlexQueryAsync(options);
        result.Data.Should().BeEmpty();
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
                    Function = AggregateFunction.Sum,
                    Alias = "totalSum"
                }
            ],
            Paging = { Page = page, PageSize = pageSize }
        };
    }

    private static QueryOptions MultiAggregateGroupedOptions(int page, int pageSize)
    {
        return new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates =
            [
                new AggregateModel { Field = "Total", Function = AggregateFunction.Sum, Alias = "totalSum" },
                new AggregateModel { Field = "Total", Function = AggregateFunction.Avg, Alias = "totalAvg" },
                new AggregateModel { Field = "Total", Function = AggregateFunction.Min, Alias = "totalMin" },
                new AggregateModel { Field = "Total", Function = AggregateFunction.Max, Alias = "totalMax" },
                new AggregateModel { Field = "Id", Function = AggregateFunction.Count, Alias = "idCount" }
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
                new AggregateModel
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
        options.Having = new HavingCondition
        {
            Field = "Total",
            Function = AggregateFunction.Sum,
            Operator = FilterOperators.GreaterThan,
            Value = minimumTotal.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        return options;
    }

    private static FilterGroup GroupOrderFilter()
    {
        return NumberPrefixFilter("GROUP-");
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
        var orders = rows
            .Select(row => new SqlOrder
            {
                Id = row.Id,
                Number = $"GROUP-{row.Id}",
                CustomerId = row.CustomerId,
                Total = row.Total,
                OrderDate = DateTime.SpecifyKind(DateTime.Parse(row.Date), DateTimeKind.Utc)
            })
            .ToList();

        _db.Orders.AddRange(orders);
        await _db.SaveChangesAsync();
    }

    private async Task AddOrdersWithNumberPrefixAsync(
        string prefix,
        params (int Id, int CustomerId, string Date, decimal Total)[] rows)
    {
        var orders = rows
            .Select(row => new SqlOrder
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

    private static T Read<T>(object row, string propertyName)
    {
        if (row is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(propertyName, out var readOnlyValue))
        {
            if (readOnlyValue is null)
                return default!;

            return (T)Convert.ChangeType(readOnlyValue, typeof(T));
        }

        if (row is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(propertyName, out var value))
        {
            if (value is null)
                return default!;

            return (T)Convert.ChangeType(value, typeof(T));
        }

        var property = row.GetType().GetProperty(
            propertyName,
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.IgnoreCase);

        property.Should().NotBeNull(
            $"the grouped row type {row.GetType().Name} should contain {propertyName}; available properties are "
            + string.Join(", ", row.GetType().GetProperties().Select(candidate => candidate.Name)));

        var propertyValue = property!.GetValue(row);
        if (propertyValue is null)
            return default!;

        return (T)Convert.ChangeType(propertyValue, typeof(T));
    }

    [Fact]
    public async Task ExecutePipeline_EFCore_OrdersClausesCorrectly()
    {
        await AddOrdersWithNumberPrefixAsync(
            "ORDER",
            (100, 1, "2026-12-01", 100m),
            (101, 1, "2026-12-02", 50m));

        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Number", Operator = "startswith", Value = "ORDER-" }]
            },
            GroupBy = ["CustomerId"],
            Aggregates = [new AggregateModel { Function = AggregateFunction.Sum, Field = "Total", Alias = "totalSum" }],
            Having = new HavingCondition { Function = AggregateFunction.Sum, Field = "Total", Operator = "gt", Value = "0" },
            Sort = [new SortNode { Field = "totalSum", Descending = true }],
            Paging = { Page = 1, PageSize = 10 }
        };

        var result = await _db.Orders.FlexQueryAsync(options);

        result.Data.Should().NotBeEmpty();
        result.Data.Should().HaveCount(1);
        Read<int>(result.Data[0], "CustomerId").Should().Be(1);
    }

    [Fact]
    public void Normalize_IncludesConsolidation_PipelineProducesCorrectQuery()
    {
        var options = new QueryOptions
        {
            Paging = { Page = 3, PageSize = 10 },
            Includes = ["Orders"]
        };

        options = options.Normalize();

        options.Paging.PageSize.Should().Be(10);
        options.Paging.Page.Should().Be(3);
        options.Includes.Should().BeNull();
        options.Expand.Should().ContainSingle(i => i.Path == "Orders");
    }
}
