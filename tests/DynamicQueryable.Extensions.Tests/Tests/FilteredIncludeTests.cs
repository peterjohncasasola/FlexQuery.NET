using DynamicQueryable.Extensions;
using DynamicQueryable.Extensions.EFCore;
using DynamicQueryable.Models;
using DynamicQueryable.Parsers;
using DynamicQueryable.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DynamicQueryable.Tests.Tests;

public class FilteredIncludeTests : IDisposable
{
    private readonly SqlProjectionDbContext _db = SqlProjectionDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ApplyFilteredIncludes_ParsesAndAppliesWhereCorrectly()
    {
        // Act
        // We only want Customer "Alice" (Id=1)
        var options = QueryOptionsParser.Parse(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["filter"] = "Id:eq:1",
            // For Alice, she has two orders:
            // - SO-001 (Total = 125.50) with Items: SKU-AAA, SKU-BBB
            // - SO-002 (Total = 45.00) with Items: SKU-CCC
            // We'll filter include to only include orders > 100, and their items with SKU-BBB
            ["include"] = "Orders(Total > 100).Items(Sku = 'SKU-BBB')"
        });

        // Use the dual pipeline:
        var result = await _db.Customers
            .AsNoTracking()
            .ApplyQueryOptions(options)     // filter root
            .ApplyFilteredIncludes(options) // filter includes
            .ToListAsync();

        // Assert
        result.Should().HaveCount(1);
        var customer = result[0];
        
        customer.Name.Should().Be("Alice");

        // The include should only bring in Orders with Total > 100
        customer.Orders.Should().HaveCount(1);
        var order = customer.Orders.First();
        order.Number.Should().Be("SO-001");
        order.Total.Should().Be(125.50m);

        // The nested include should only bring in OrderItems with Sku = "SKU-BBB"
        order.Items.Should().HaveCount(1);
        order.Items.First().Sku.Should().Be("SKU-BBB");
    }

    [Fact]
    public async Task ApplyFilteredInclude_WithApplySelect_FiltersProjectedCollection()
    {
        // Arrange
        var options = QueryOptionsParser.Parse(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["filter"] = "Id:eq:1",
            ["include"] = "Orders(Total > 100)",
            ["select"] = "Id,Name,Orders.Number,Orders.Total"
        });

        // Act
        var result = await _db.Customers
            .AsNoTracking()
            .ApplyQueryOptions(options)
            .ApplySelect(options)
            .ToListAsync();

        // Assert
        result.Should().HaveCount(1);
        var alice = result[0];
        
        var name = alice.GetType().GetProperty("Name")?.GetValue(alice) as string;
        name.Should().Be("Alice");

        // Orders should be filtered to only those with Total > 100
        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        orders.Should().NotBeNull();

        var orderList = new List<object>();
        foreach (var o in orders!) orderList.Add(o);

        orderList.Should().HaveCount(1);
        
        var firstOrder = orderList[0];
        var orderNumber = firstOrder.GetType().GetProperty("Number")?.GetValue(firstOrder) as string;
        var orderTotal = firstOrder.GetType().GetProperty("Total")?.GetValue(firstOrder);

        orderNumber.Should().Be("SO-001");
        orderTotal.Should().Be(125.50m);
    }
}
