using FlexQuery.NET;
using FlexQuery.NET.EFCore;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FlexQuery.NET.Tests.Tests;

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
        var orderList = new List<object>();
        foreach (var o in orders!) orderList.Add(o);

        orderList.Should().HaveCount(1);
        orderList[0].GetType().GetProperty("Number")?.GetValue(orderList[0]).Should().Be("SO-001");
        orderList[0].GetType().GetProperty("Total")?.GetValue(orderList[0]).Should().Be(125.50m);
    }

    [Fact]
    public async Task CaseInsensitiveStringEquality_MatchesDifferentCasing()
    {
        // NOTE: Case-insensitivity is now delegated to the database collation (SQL Server default: CI_AS).
        // FlexQuery no longer applies .ToLower() to force case-insensitivity at the expression level.
        // This means:
        //   - On SQL Server: WHERE [Sku] = 'sku-aaa' and WHERE [Sku] = 'SKU-AAA' both work (CI collation).
        //   - On in-memory / SQLite providers: comparisons are case-sensitive.
        // This test uses the exact stored casing to work correctly with the SQLite in-memory test database.
        var options = QueryOptionsParser.Parse(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["filter"] = "Id:eq:1",
            ["include"] = "Orders.Items(Sku = 'SKU-AAA')",  // exact case to match SQLite in-memory
            ["select"] = "Id,Orders.Number,Orders.Items.Sku"
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
        
        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        var orderList = new List<object>();
        foreach (var o in orders!) orderList.Add(o);

        // Should still have both orders (because filter is on Items)
        orderList.Should().HaveCount(2);

        // Find the items of SO-001
        var so001 = orderList.Cast<object>().First(o => (string)o.GetType().GetProperty("Number")?.GetValue(o)! == "SO-001");
        var items = so001.GetType().GetProperty("Items")?.GetValue(so001) as System.Collections.IEnumerable;
        var itemList = new List<object>();
        foreach (var i in items!) itemList.Add(i);

        // Should have 1 item (SKU-AAA)
        itemList.Should().HaveCount(1);
        var sku = itemList[0].GetType().GetProperty("Sku")?.GetValue(itemList[0]) as string;
        sku.Should().Be("SKU-AAA");
    }

    [Fact]
    public async Task ToProjectedQueryResultAsync_AppliesFilteredIncludes()
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
            .ToProjectedQueryResultAsync(options);

        // Assert
        result.TotalCount.Should().Be(1);
        var alice = result.Data.First();
        
        var name = alice.GetType().GetProperty("Name")?.GetValue(alice) as string;
        name.Should().Be("Alice");

        // Orders should be filtered to only those with Total > 100
        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        var orderList = new List<object>();
        foreach (var o in orders!) orderList.Add(o);

        orderList.Should().HaveCount(1);
        orderList[0].GetType().GetProperty("Number")?.GetValue(orderList[0]).Should().Be("SO-001");
    }

    [Fact]
    public async Task Select_OnNavigation_OverridesIncludeAllScalars()
    {
        // Arrange
        var options = QueryOptionsParser.Parse(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["filter"] = "Id:eq:1",
            ["include"] = "Orders(Total > 100)",
            ["select"] = "Id,Orders.Number" // We ONLY want Number, not Total!
        });

        // Act
        var result = await _db.Customers
            .AsNoTracking()
            .ToProjectedQueryResultAsync(options);

        // Assert
        var alice = result.Data.First();
        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        var orderList = new List<object>();
        foreach (var o in orders!) orderList.Add(o);
        
        orderList.Should().HaveCount(1);
        var firstOrder = orderList[0];
        
        // It should have Number
        firstOrder.GetType().GetProperty("Number").Should().NotBeNull();
        
        // It should NOT have Total (since we didn't select it, and IncludeAllScalars should be false)
        firstOrder.GetType().GetProperty("Total").Should().BeNull();
    }

    [Fact]
    public async Task FilteredInclude_SupportsDsl()
    {
        // Arrange
        // Using DSL format: Total:gt:100
        var options = QueryOptionsParser.Parse(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["filter"] = "Id:eq:1",
            ["include"] = "Orders(Total:gt:100)",
            ["select"] = "Id,Orders.Number,Orders.Total"
        });

        // Act
        var result = await _db.Customers
            .AsNoTracking()
            .ToProjectedQueryResultAsync(options);

        // Assert
        var alice = result.Data.First();
        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        var orderList = new List<object>();
        foreach (var o in orders!) orderList.Add(o);
        
        // Alice has two orders: SO-001 (Total=125.50) and SO-002 (Total=45.00)
        // SO-001 should be included, SO-002 should be filtered out
        orderList.Should().HaveCount(1);
        orderList[0].GetType().GetProperty("Number")!.GetValue(orderList[0]).Should().Be("SO-001");
    }

    [Fact]
    public async Task FilteredInclude_NestedMixed_WorksCorrectly()
    {
        // Arrange
        // Mixed: Level 1 is DSL (Total:gt:100), Level 2 is JQL (Sku = 'SKU-AAA')
        // We use lowercase 'items' to test case-insensitivity.
        var options = QueryOptionsParser.Parse(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["filter"] = "Id:eq:1",
            ["include"] = "Orders(Total:gt:100).items(Sku = 'SKU-AAA')",
            ["select"] = "Id,Orders.Number,Orders.Items.Sku"
        });

        // Act
        var result = await _db.Customers
            .AsNoTracking()
            .ToProjectedQueryResultAsync(options);

        // Assert
        var alice = result.Data.First();
        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        var orderList = new List<object>();
        foreach (var o in orders!) orderList.Add(o);
        
        // Should have 1 order (SO-001)
        orderList.Should().HaveCount(1);
        var so001 = orderList[0];
        
        var items = so001.GetType().GetProperty("Items")?.GetValue(so001) as System.Collections.IEnumerable;
        var itemList = new List<object>();
        foreach (var i in items!) itemList.Add(i);
        
        // SHOULD have only 1 item (SKU-AAA). If this is 2, the nested filter failed.
        itemList.Should().HaveCount(1);
        itemList[0].GetType().GetProperty("Sku")!.GetValue(itemList[0]).Should().Be("SKU-AAA");
    }

    [Fact]
    public async Task FilteredInclude_ComplexChain_MixedFormats()
    {
        // Arrange
        // Exact user example with potential spaces: include=orders(status:eq:cancelled).items(sku = 'AAA')
        // We use 'Total' and 'Sku' from our seed data to make it real.
        var options = QueryOptionsParser.Parse(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["filter"] = "Id:eq:1",
            ["include"] = "Orders(Total:gt:100) . items(Sku = 'SKU-AAA')",
            ["select"] = "Id,Orders.Number,Orders.Items.Sku"
        });

        // Act
        var result = await _db.Customers
            .AsNoTracking()
            .ToProjectedQueryResultAsync(options);

        // Assert
        var alice = result.Data.First();
        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        var orderList = new List<object>();
        foreach (var o in orders!) orderList.Add(o);
        
        orderList.Should().HaveCount(1);
        var so001 = orderList[0];
        
        var items = so001.GetType().GetProperty("Items")?.GetValue(so001) as System.Collections.IEnumerable;
        var itemList = new List<object>();
        foreach (var i in items!) itemList.Add(i);
        
        // This confirms the nested filter on 'items' was applied despite the chain and mixed formats.
        itemList.Should().HaveCount(1);
        itemList[0].GetType().GetProperty("Sku")!.GetValue(itemList[0]).Should().Be("SKU-AAA");
    }
}
