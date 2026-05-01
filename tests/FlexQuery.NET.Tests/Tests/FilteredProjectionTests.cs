using FlexQuery.NET.Constants;
using FlexQuery.NET;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace FlexQuery.NET.Tests.Tests;

public sealed class FilteredProjectionTests : IDisposable
{
    private readonly SqlProjectionDbContext _db = SqlProjectionDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Projection_FilteredChildCollection_SingleNestedFilter()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters =
                [
                    new FilterCondition
                    {
                        Field = "Orders.Number",
                        Operator = FilterOperators.Equal,
                        Value = "SO-001"
                    }
                ]
            },
            Select = ["Id", "Orders.Number"]
        };

        var rows = await _db.Customers
            .ApplyQueryOptions(options)
            .ApplySelect(options)
            .ToListAsync();

        rows.Should().HaveCount(1);

        var customer = rows[0];
        var orders = ((System.Collections.IEnumerable)customer.GetType().GetProperty("Orders")!.GetValue(customer)!)
            .Cast<object>()
            .ToList();

        orders.Should().HaveCount(1);
        orders[0].GetType().GetProperty("Number")!.GetValue(orders[0]).Should().Be("SO-001");
    }

    [Fact]
    public async Task Projection_FilteredChildCollection_MultipleNestedFilters_And()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Logic = LogicOperator.And,
                Filters =
                [
                    new FilterCondition { Field = "Orders.Number", Operator = FilterOperators.Contains, Value = "SO-" },
                    new FilterCondition { Field = "Orders.Total", Operator = FilterOperators.GreaterThan, Value = "100" }
                ]
            },
            Select = ["Id", "Orders.Number", "Orders.Total"]
        };

        var rows = await _db.Customers
            .ApplyQueryOptions(options)
            .ApplySelect(options)
            .ToListAsync();

        rows.Should().HaveCount(1);

        var customer = rows[0];
        var orders = ((System.Collections.IEnumerable)customer.GetType().GetProperty("Orders")!.GetValue(customer)!)
            .Cast<object>()
            .ToList();

        orders.Should().ContainSingle();
        orders[0].GetType().GetProperty("Number")!.GetValue(orders[0]).Should().Be("SO-001");
    }

    [Fact]
    public async Task Projection_FilteredChildCollection_OrWithinCollection_IsSupported()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Logic = LogicOperator.Or,
                Filters =
                [
                    new FilterCondition { Field = "Orders.Number", Operator = FilterOperators.Equal, Value = "SO-001" },
                    new FilterCondition { Field = "Orders.Number", Operator = FilterOperators.Equal, Value = "SO-002" }
                ]
            },
            Select = ["Id", "Orders.Number"]
        };

        var rows = await _db.Customers
            .ApplyQueryOptions(options)
            .ApplySelect(options)
            .ToListAsync();

        rows.Should().HaveCount(1);

        var customer = rows[0];
        var orders = ((System.Collections.IEnumerable)customer.GetType().GetProperty("Orders")!.GetValue(customer)!)
            .Cast<object>()
            .ToList();

        orders.Should().HaveCount(2);
        orders.Select(o => (string)o.GetType().GetProperty("Number")!.GetValue(o)!)
            .Should().BeEquivalentTo(["SO-001", "SO-002"]);
    }

    [Fact]
    public async Task Projection_NoSelect_DoesNotApplyChildFiltering()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters =
                [
                    new FilterCondition { Field = "Orders.Number", Operator = FilterOperators.Equal, Value = "SO-001" }
                ]
            }
            // Select omitted on purpose
        };

        // ApplySelect with no select returns original entity cast to object.
        var rows = await _db.Customers
            .ApplyQueryOptions(options)
            .ApplySelect(options)
            .ToListAsync();

        rows.Should().HaveCount(1);

        // Without a projection, we shouldn't attempt to filter child collections.
        var customer = (SqlCustomer)rows[0];
        customer.Orders.Should().HaveCount(2);
    }

    [Fact]
    public async Task Projection_FilteredChildCollection_DeepNestedCollection_IsHandledRecursively()
    {
        // Filters orders that have any item with SKU-AAA; and if orders/items are selected,
        // the projected Orders should be filtered to only matching orders, and Items should be filtered too.
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters =
                [
                    new FilterCondition { Field = "Orders.Items.Sku", Operator = FilterOperators.Equal, Value = "SKU-AAA" }
                ]
            },
            Select = ["Id", "Orders.Number", "Orders.Items.Sku"]
        };

        var rows = await _db.Customers
            .ApplyQueryOptions(options)
            .ApplySelect(options)
            .ToListAsync();

        rows.Should().HaveCount(1);

        var customer = rows[0];
        var orders = ((System.Collections.IEnumerable)customer.GetType().GetProperty("Orders")!.GetValue(customer)!)
            .Cast<object>()
            .ToList();

        orders.Should().ContainSingle();

        var order = orders[0];
        var items = ((System.Collections.IEnumerable)order.GetType().GetProperty("Items")!.GetValue(order)!)
            .Cast<object>()
            .ToList();

        items.Should().ContainSingle();
        items[0].GetType().GetProperty("Sku")!.GetValue(items[0]).Should().Be("SKU-AAA");
    }

    [Fact]
    public void Projection_FilteredChildCollection_GeneratesMinimalSqlShape()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters =
                [
                    new FilterCondition { Field = "Orders.Number", Operator = FilterOperators.Equal, Value = "SO-001" }
                ]
            },
            Select = ["Id", "Orders.Number"]
        };

        var query = _db.Customers
            .ApplyQueryOptions(options)
            .ApplySelect(options);

        var sql = query.ToQueryString();

        // Baseline expectation:
        // - One reference from the root query
        // - One reference from the correlated subquery/materialization for Orders projection
        // - (Optionally) one additional from the EXISTS predicate used to filter parent rows
        // Keep this lenient but bounded so we catch accidental duplication.
        Regex.Matches(sql, "\"Orders\"").Count.Should().BeLessOrEqualTo(3);
    }
}

