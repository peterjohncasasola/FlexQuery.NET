using System.Reflection;
using FlexQuery.NET.Dapper.Sql;
using FlexQuery.NET.EntityFrameworkCore;

using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Paging;


namespace FlexQuery.NET.Tests.Integration;



public class GroupedQueryEfCoreTests : IDisposable

{

    private readonly SqlProjectionDbContext _db = SqlProjectionDbContext.CreateSeeded();



    public void Dispose() => _db.Dispose();



    private T Read<T>(object row, string propertyName)

    {

        if (row is IReadOnlyDictionary<string, object?> readOnlyDictionary

            && readOnlyDictionary.TryGetValue(propertyName, out var readOnlyValue))

        {

            if (readOnlyValue is null) return default!;

            return (T)Convert.ChangeType(readOnlyValue!, typeof(T))!;

        }



        if (row is IDictionary<string, object?> dictionary

            && dictionary.TryGetValue(propertyName, out var value))

        {

            if (value is null) return default!;

            return (T)Convert.ChangeType(value!, typeof(T))!;

        }



        var property = row.GetType().GetProperty(

            propertyName,

            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);



        property.Should().NotBeNull(

            "grouped row type " + row.GetType().Name + " should contain " + propertyName + "; "

            + "available: " + string.Join(", ", row.GetType().GetProperties().Select(p => p.Name)));



        var propertyValue = property!.GetValue(row);

        if (propertyValue is null) return default!;

        return (T)Convert.ChangeType(propertyValue, typeof(T))!;

    }



    [Fact]

    public async Task GroupedQuery_InvalidDetailSort_SortIsIgnored()

    {

        var options = new QueryOptions

        {

            GroupBy = ["CustomerId"],

            Aggregates = [new Aggregate { Field = "Total", Function = AggregateFunction.Sum, Alias = "totalSum" }],

            Sort = [new SortNode { Field = "Id" }],

            Paging = { Disabled = true }

        };



        var result = await _db.Orders.FlexQueryAsync(options);

        result.Data.Should().NotBeEmpty();

        result.Data.Should().AllSatisfy(row =>

        {

            Read<int>(row, "CustomerId").Should().BeGreaterThan(0);

            Read<double>(row, "totalSum").Should().BeGreaterThan(0);

        });

    }



    [Fact]

    public async Task GroupedQuery_AllInvalidSorts_FallsBackToGroupKey()

    {

        var options = new QueryOptions

        {

            GroupBy = ["CustomerId"],

            Aggregates = [new Aggregate { Field = "Total", Function = AggregateFunction.Sum, Alias = "totalSum" }],

            Sort = [

                new SortNode { Field = "Id" },

                new SortNode { Field = "Number" }

            ],

            Paging = { Disabled = true }

        };



        var result = await _db.Orders.FlexQueryAsync(options);

        result.Data.Should().NotBeEmpty();

        result.Data.Select(row => Read<int>(row, "CustomerId")).Should().BeInAscendingOrder();

    }



    [Fact]

    public async Task GroupedQuery_AggregateAliasSort_SortsByAlias()

    {

        var options = new QueryOptions

        {

            GroupBy = ["CustomerId"],

            Aggregates = [new Aggregate { Field = "Total", Function = AggregateFunction.Sum, Alias = "totalSum" }],

            Sort = [new SortNode { Field = "totalSum", Descending = true }],

            Paging = { Disabled = true }

        };



        var result = await _db.Orders.FlexQueryAsync(options);

        result.Data.Should().NotBeEmpty();

        result.Data.Select(row => Read<double>(row, "totalSum")).Should().BeInDescendingOrder();

    }



    [Fact]

    public async Task GroupedQuery_AggregateFieldSort_ResolvesToAlias()

    {

        var options = new QueryOptions

        {

            GroupBy = ["CustomerId"],

            Aggregates = [new Aggregate { Field = "Total", Function = AggregateFunction.Sum, Alias = "totalSum" }],

            Sort = [new SortNode { Field = "Total", Descending = true }],

            Paging = { Disabled = true }

        };



        var result = await _db.Orders.FlexQueryAsync(options);

        result.Data.Should().NotBeEmpty();

        result.Data.Select(row => Read<double>(row, "totalSum")).Should().BeInDescendingOrder();

    }



    [Fact]

    public async Task GroupedQuery_GroupKeySort_SortsByKey()

    {

        var options = new QueryOptions

        {

            GroupBy = ["CustomerId"],

            Aggregates = [new Aggregate { Field = "Total", Function = AggregateFunction.Sum, Alias = "totalSum" }],

            Sort = [new SortNode { Field = "CustomerId", Descending = true }],

            Paging = { Disabled = true }

        };



        var result = await _db.Orders.FlexQueryAsync(options);

        result.Data.Should().NotBeEmpty();

        result.Data.Select(row => Read<int>(row, "CustomerId")).Should().BeInDescendingOrder();

    }



    [Fact]

    public async Task GroupedQuery_PagingWithoutSort_DefaultsToGroupKeyOrder()

    {

        await AddOrdersForPagingAsync();



        var firstPage = await _db.Orders.FlexQueryAsync(new QueryOptions

        {

            GroupBy = ["CustomerId"],

            Aggregates = [new Aggregate { Field = "Total", Function = AggregateFunction.Sum, Alias = "totalSum" }],

            Paging = { Page = 1, PageSize = 2 },

            Sort = []

        });



        var secondPage = await _db.Orders.FlexQueryAsync(new QueryOptions

        {

            GroupBy = ["CustomerId"],

            Aggregates = [new Aggregate { Field = "Total", Function = AggregateFunction.Sum, Alias = "totalSum" }],

            Paging = { Page = 2, PageSize = 2 },

            Sort = []

        });



        var firstKeys = firstPage.Data.Select(row => Read<int>(row, "CustomerId")).ToList();

        var secondKeys = secondPage.Data.Select(row => Read<int>(row, "CustomerId")).ToList();



        firstKeys.Should().NotIntersectWith(secondKeys);

        firstPage.Data.Concat(secondPage.Data)

            .Select(row => Read<int>(row, "CustomerId"))

            .Should().BeInAscendingOrder();

    }



    private async Task AddOrdersForPagingAsync()

    {

        var additional = Enumerable.Range(0, 996)

            .Select(i => new Order

            {

                Id = 2000 + i,

                Number = "PAGE-" + i.ToString("D4"),

                CustomerId = (i % 3) + 1,

                Total = 10m,

                OrderDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i)

            })

            .ToList();

        _db.Orders.AddRange(additional);

        await _db.SaveChangesAsync();

    }

}