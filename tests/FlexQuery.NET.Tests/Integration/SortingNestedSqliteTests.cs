using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Tests.Integration;

public sealed class SortingNestedSqliteTests : IDisposable
{
    private readonly SqlProjectionDbContext _db = SqlProjectionDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Sort_NestedProperty_CustomerName_Works()
    {
        var options = new QueryOptions
        {
            Sort = [new SortNode { Field = "Customer.Name", Descending = false }],
            Paging = { Disabled = true }
        };

        var result = _db.Orders
            .AsQueryable()
            .Apply(options)
            .Select(x => x.Customer.Name)
            .ToList();

        result.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Sort_MultiNestedProperty_CustomerAddressCity_Works()
    {
        var options = new QueryOptions
        {
            Sort = [new SortNode { Field = "Customer.Address.City", Descending = false }],
            Paging = { Disabled = true }
        };

        var result = _db.Orders
            .AsQueryable()
            .Apply(options)
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
                new SortNode { Field = "Customer.NoSuchPath", Descending = true },
                new SortNode { Field = "Customer.Name", Descending = false }
            ],
            Paging = { Disabled = true }
        };

        var result = _db.Orders
            .AsQueryable()
            .Apply(options)
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
                new SortNode { Field = "Orders.Total", Descending = true },
                new SortNode { Field = "Name", Descending = false }
            ],
            Paging = { Disabled = true }
        };

        var result = _db.Customers
            .AsQueryable()
            .Apply(options)
            .Select(c => c.Email)
            .ToList();

        var nonNullEmails = result.Where(e => e != null).Cast<string>().ToList();
        nonNullEmails.Should().Equal("alice@example.com", "bob@example.com", "bob2@example.com");
    }

    [Fact]
    public void Sort_Aggregate_Count_Works()
    {
        var options = new QueryOptions
        {
            Sort =
            [
                new SortNode
                {
                    Field = "Orders",
                    Aggregate = AggregateFunction.Count,
                    Descending = false
                }
            ],
            Paging = { Disabled = true }
        };

        var result = _db.Customers
            .AsQueryable()
            .Apply(options)
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
                new SortNode
                {
                    Field = "Orders",
                    Aggregate = AggregateFunction.Max,
                    AggregateField = "Total",
                    Descending = true
                }
            ],
            Paging = { Disabled = true }
        };

        var result = _db.Customers
            .AsQueryable()
            .Apply(options)
            .Select(c => c.Email)
            .ToList();

        var nonNullEmails = result.Where(e => e != null).Cast<string>().ToList();
        nonNullEmails.Should().Equal("bob@example.com", "alice@example.com", "bob2@example.com");
    }

    [Fact]
    public void Sort_Aggregate_Min_Works()
    {
        var options = new QueryOptions
        {
            Sort =
            [
                new SortNode
                {
                    Field = "Orders",
                    Aggregate = AggregateFunction.Min,
                    AggregateField = "Total",
                    Descending = false
                }
            ],
            Paging = { Disabled = true }
        };

        var result = _db.Customers
            .AsQueryable()
            .Apply(options)
            .Select(c => c.Email)
            .ToList();

        var nonNullEmails = result.Where(e => e != null).Cast<string>().ToList();
        nonNullEmails.Should().Equal("bob2@example.com", "alice@example.com", "bob@example.com");
    }

    [Fact]
    public void Sort_Aggregate_Avg_Works()
    {
        var options = new QueryOptions
        {
            Sort =
            [
                new SortNode
                {
                    Field = "Orders",
                    Aggregate = AggregateFunction.Avg,
                    AggregateField = "Total",
                    Descending = true
                }
            ],
            Paging = { Disabled = true }
        };

        var result = _db.Customers
            .AsQueryable()
            .Apply(options)
            .Select(c => c.Email)
            .ToList();

        var nonNullEmails = result.Where(e => e != null).Cast<string>().ToList();
        nonNullEmails.Should().Equal("bob@example.com", "alice@example.com", "bob2@example.com");
    }

    [Fact]
    public void Sort_Mixed_NormalAndAggregate_Works()
    {
        var query = new Dictionary<string, StringValues>
        {
            ["sort"] = new("name:asc,sum:orders.total:desc")
        };

        var options = QueryOptionsParser.Parse(query);

        var result = _db.Customers
            .AsQueryable()
            .Apply(options)
            .Select(c => new { c.Name, c.Email })
            .ToList();

        result.Select(x => x.Name).Should().BeInAscendingOrder();

        var bobs = result.Where(x => x.Name == "Bob Smith").ToList();
        bobs.Select(x => x.Email).Should().Equal("bob@example.com");
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
            .Apply(options)
            .Select(c => c.Name)
            .ToList();

        result.Should().BeInAscendingOrder();
    }
}
