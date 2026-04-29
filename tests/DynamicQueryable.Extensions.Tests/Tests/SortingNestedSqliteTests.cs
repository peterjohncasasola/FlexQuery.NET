using DynamicQueryable.Extensions;
using DynamicQueryable.Models;
using DynamicQueryable.Parsers;
using DynamicQueryable.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Primitives;

namespace DynamicQueryable.Tests.Tests;

public sealed class SortingNestedSqliteTests : IDisposable
{
    private readonly SqlProjectionDbContext _db = SqlProjectionDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Sort_NestedProperty_CustomerName_Works()
    {
        var options = new QueryOptions
        {
            Sort = [new SortOption { Field = "Customer.Name", Descending = false }],
            Paging = { Disabled = true }
        };

        var result = _db.Orders
            .AsQueryable()
            .ApplyQueryOptions(options)
            .Select(x => x.Customer.Name)
            .ToList();

        result.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Sort_MultiNestedProperty_CustomerAddressCity_Works()
    {
        var options = new QueryOptions
        {
            Sort = [new SortOption { Field = "Customer.Address.City", Descending = false }],
            Paging = { Disabled = true }
        };

        var result = _db.Orders
            .AsQueryable()
            .ApplyQueryOptions(options)
            .Select(x => x.Customer.Address!.City)
            .ToList();

        result.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Sort_InvalidPath_IsIgnored()
    {
        var options = new QueryOptions
        {
            Sort =
            [
                new SortOption { Field = "Customer.NoSuchPath", Descending = true },
                new SortOption { Field = "Customer.Name", Descending = false }
            ],
            Paging = { Disabled = true }
        };

        var result = _db.Orders
            .AsQueryable()
            .ApplyQueryOptions(options)
            .Select(x => x.Customer.Name)
            .ToList();

        result.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Sort_CollectionNavigation_IsSkipped()
    {
        var options = new QueryOptions
        {
            Sort =
            [
                new SortOption { Field = "Orders.Total", Descending = true },
                new SortOption { Field = "Name", Descending = false }
            ],
            Paging = { Disabled = true }
        };

        var result = _db.Customers
            .AsQueryable()
            .ApplyQueryOptions(options)
            .Select(x => x.Name)
            .ToList();

        result.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Sort_ParsedFromQueryString_NestedAndMultiNested_WorksEndToEnd()
    {
        var query = new Dictionary<string, StringValues>
        {
            ["sort"] = new("customer.name:asc,customer.address.city:desc"),
            ["pageSize"] = new("100")
        };

        var options = QueryOptionsParser.Parse(query);

        var result = _db.Orders
            .AsQueryable()
            .ApplyQueryOptions(options)
            .Select(x => new
            {
                CustomerName = x.Customer.Name,
                City = x.Customer.Address!.City
            })
            .ToList();

        result.Should().HaveCountGreaterThan(1);
        result.Select(x => x.CustomerName).Should().BeInAscendingOrder();
    }

    [Fact]
    public void Sort_Aggregate_Sum_Works()
    {
        var options = new QueryOptions
        {
            Sort =
            [
                new SortOption
                {
                    Field = "Orders",
                    Aggregate = "sum",
                    AggregateField = "Total",
                    Descending = true
                }
            ],
            Paging = { Disabled = true }
        };

        var result = _db.Customers
            .AsQueryable()
            .ApplyQueryOptions(options)
            .Select(c => c.Email)
            .ToList();

        result.Should().Equal("alice@example.com", "bob@example.com", "bob2@example.com");
    }

    [Fact]
    public void Sort_Aggregate_Count_Works()
    {
        var options = new QueryOptions
        {
            Sort =
            [
                new SortOption
                {
                    Field = "Orders",
                    Aggregate = "count",
                    Descending = false
                }
            ],
            Paging = { Disabled = true }
        };

        var result = _db.Customers
            .AsQueryable()
            .ApplyQueryOptions(options)
            .Select(c => c.Email)
            .ToList();

        result.Last().Should().Be("alice@example.com");
    }

    [Fact]
    public void Sort_Aggregate_Max_Works()
    {
        var options = new QueryOptions
        {
            Sort =
            [
                new SortOption
                {
                    Field = "Orders",
                    Aggregate = "max",
                    AggregateField = "Total",
                    Descending = true
                }
            ],
            Paging = { Disabled = true }
        };

        var result = _db.Customers
            .AsQueryable()
            .ApplyQueryOptions(options)
            .Select(c => c.Email)
            .ToList();

        result.Should().Equal("alice@example.com", "bob@example.com", "bob2@example.com");
    }

    [Fact]
    public void Sort_Aggregate_Min_Works()
    {
        var options = new QueryOptions
        {
            Sort =
            [
                new SortOption
                {
                    Field = "Orders",
                    Aggregate = "min",
                    AggregateField = "Total",
                    Descending = false
                }
            ],
            Paging = { Disabled = true }
        };

        var result = _db.Customers
            .AsQueryable()
            .ApplyQueryOptions(options)
            .Select(c => c.Email)
            .ToList();

        result.Should().Equal("bob2@example.com", "alice@example.com", "bob@example.com");
    }

    [Fact]
    public void Sort_Aggregate_Avg_Works()
    {
        var options = new QueryOptions
        {
            Sort =
            [
                new SortOption
                {
                    Field = "Orders",
                    Aggregate = "avg",
                    AggregateField = "Total",
                    Descending = true
                }
            ],
            Paging = { Disabled = true }
        };

        var result = _db.Customers
            .AsQueryable()
            .ApplyQueryOptions(options)
            .Select(c => c.Email)
            .ToList();

        result.Should().Equal("alice@example.com", "bob@example.com", "bob2@example.com");
    }

    [Fact]
    public void Sort_Mixed_NormalAndAggregate_Works()
    {
        var query = new Dictionary<string, StringValues>
        {
            ["sort"] = new("name:asc,orders.sum(total):desc")
        };

        var options = QueryOptionsParser.Parse(query);

        var result = _db.Customers
            .AsQueryable()
            .ApplyQueryOptions(options)
            .Select(c => new { c.Name, c.Email })
            .ToList();

        result.Select(x => x.Name).Should().BeInAscendingOrder();

        var bobs = result.Where(x => x.Name == "Bob").ToList();
        bobs.Select(x => x.Email).Should().Equal("bob@example.com", "bob2@example.com");
    }

    [Fact]
    public void Sort_CollectionWithoutAggregate_IsSkipped()
    {
        var query = new Dictionary<string, StringValues>
        {
            ["sort"] = new("orders.total:desc,name:asc")
        };

        var options = QueryOptionsParser.Parse(query);

        var result = _db.Customers
            .AsQueryable()
            .ApplyQueryOptions(options)
            .Select(c => c.Name)
            .ToList();

        result.Should().BeInAscendingOrder();
    }
}
