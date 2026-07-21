using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Tests.Integration;

public class FilteredIncludeTests : IDisposable
{
    private readonly SqlProjectionDbContext _db = SqlProjectionDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Include_WithExpandFilter_AppliesFilteredInclude()
    {
        var parameters = new FlexQueryParameters
        {
            Filter = "Id:eq:1",
            Include = "Orders",
            Expand = "Orders(filter=Total:gt:100)"
        };

        var result = await _db.Customers
            .AsNoTracking()
            .FlexQueryAsync(parameters);

        result.TotalCount.Should().Be(1);
        var alice = result.Data.First();
        
        alice.GetType().GetProperty("Name")?.GetValue(alice).Should().Be("Alice Johnson");

        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        var orderList = new List<object>();
        foreach (var o in orders!) orderList.Add(o);

        orderList.Should().HaveCount(1);
        var order = orderList[0];
        order.GetType().GetProperty("Number")?.GetValue(order).Should().Be("SO-001");
        order.GetType().GetProperty("Total")?.GetValue(order).Should().Be(150.0m);
    }

    [Fact]
    public async Task Include_WithExpandFilter_AndSelect_FiltersProjectedCollection()
    {
        var parameters = new FlexQueryParameters
        {
            Filter = "Id:eq:1",
            Include = "Orders",
            Expand = "Orders(filter=Total:gt:100)",
            Select = "Id,Name,Orders.Number,Orders.Total"
        };

        var result = await _db.Customers
            .AsNoTracking()
            .FlexQueryAsync(parameters);

        result.TotalCount.Should().Be(1);
        var alice = result.Data.First();
        
        var name = alice.GetType().GetProperty("Name")?.GetValue(alice) as string;
        name.Should().Be("Alice Johnson");

        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        var orderList = new List<object>();
        foreach (var o in orders!) orderList.Add(o);

        orderList.Should().HaveCount(1);
        orderList[0].GetType().GetProperty("Number")?.GetValue(orderList[0]).Should().Be("SO-001");
        orderList[0].GetType().GetProperty("Total")?.GetValue(orderList[0]).Should().Be(150.0m);
    }

    [Fact]
    public async Task Include_WithNestedExpandFilter_CaseInsensitiveMatch()
    {
        var parameters = new FlexQueryParameters
        {
            Filter = "Id:eq:1",
            Include = "Orders,Orders.OrderItems",
            Expand = "Orders(OrderItems(filter=Sku:eq:SKU-AAA))",
            Select = "Id,Orders.Number,Orders.OrderItems.Sku"
        };

        var result = await _db.Customers
            .AsNoTracking()
            .FlexQueryAsync(parameters);

        result.TotalCount.Should().Be(1);
        var alice = result.Data.First();
        
        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        var orderList = new List<object>();
        foreach (var o in orders!) orderList.Add(o);

        orderList.Should().HaveCount(2);

        var so001 = orderList.First(o => (string)o.GetType().GetProperty("Number")?.GetValue(o)! == "SO-001");
        var items = so001.GetType().GetProperty("OrderItems")?.GetValue(so001) as System.Collections.IEnumerable;
        var itemList = new List<object>();
        foreach (var i in items!) itemList.Add(i);

        itemList.Should().HaveCount(1);
        var sku = itemList[0].GetType().GetProperty("Sku")?.GetValue(itemList[0]) as string;
        sku.Should().Be("SKU-AAA");
    }

    [Fact]
    public async Task Include_WithExpandFilter_ToProjectedQueryResult()
    {
        var parameters = new FlexQueryParameters
        {
            Filter = "Id:eq:1",
            Include = "Orders",
            Expand = "Orders(filter=Total:gt:100)",
            Select = "Id,Name,Orders.Number,Orders.Total"
        };

        var result = await _db.Customers
            .AsNoTracking()
            .FlexQueryAsync(parameters);

        result.TotalCount.Should().Be(1);
        var alice = result.Data.First();
        
        var name = alice.GetType().GetProperty("Name")?.GetValue(alice) as string;
        name.Should().Be("Alice Johnson");

        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        var orderList = new List<object>();
        foreach (var o in orders!) orderList.Add(o);

        orderList.Should().HaveCount(1);
        orderList[0].GetType().GetProperty("Number")?.GetValue(orderList[0]).Should().Be("SO-001");
    }

    [Fact]
    public async Task Include_WithExpandFilter_SelectOnNavigation_OverridesIncludeAllScalars()
    {
        var parameters = new FlexQueryParameters
        {
            Filter = "Id:eq:1",
            Include = "Orders",
            Expand = "Orders(filter=Total:gt:100)",
            Select = "Id,Orders.Number"
        };

        var result = await _db.Customers
            .AsNoTracking()
            .FlexQueryAsync(parameters);

        var alice = result.Data.First();
        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        var orderList = new List<object>();
        foreach (var o in orders!) orderList.Add(o);
        
        orderList.Should().HaveCount(1);
        var firstOrder = orderList[0];
        
        firstOrder.GetType().GetProperty("Number").Should().NotBeNull();
        firstOrder.GetType().GetProperty("Total").Should().BeNull();
    }

    [Fact]
    public async Task Include_WithExpandFilter_SupportsDsl()
    {
        var parameters = new FlexQueryParameters
        {
            Filter = "Id:eq:1",
            Include = "Orders",
            Expand = "Orders(filter=Total:gt:100)",
            Select = "Id,Orders.Number,Orders.Total"
        };

        var result = await _db.Customers
            .AsNoTracking()
            .FlexQueryAsync(parameters);

        var alice = result.Data.First();
        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        var orderList = new List<object>();
        foreach (var o in orders!) orderList.Add(o);
        
        orderList.Should().HaveCount(1);
        orderList[0].GetType().GetProperty("Number")!.GetValue(orderList[0]).Should().Be("SO-001");
    }

    [Fact]
    public async Task Include_WithNestedExpandFilter_WorksCorrectly()
    {
        var parameters = new FlexQueryParameters
        {
            Filter = "Id:eq:1",
            Include = "Orders,Orders.OrderItems",
            Expand = "Orders(filter=Total:gt:100,OrderItems(filter=Sku:eq:SKU-BBB))",
            Select = "Id,Orders.Number,Orders.OrderItems.Sku"
        };

        var result = await _db.Customers
            .AsNoTracking()
            .FlexQueryAsync(parameters);

        var alice = result.Data.First();
        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        var orderList = new List<object>();
        foreach (var o in orders!) orderList.Add(o);
        
        orderList.Should().HaveCount(1);
        var so001 = orderList[0];
        
        var items = so001.GetType().GetProperty("OrderItems")?.GetValue(so001) as System.Collections.IEnumerable;
        var itemList = new List<object>();
        foreach (var i in items!) itemList.Add(i);
        
        itemList.Should().HaveCount(1);
        itemList[0].GetType().GetProperty("Sku")!.GetValue(itemList[0]).Should().Be("SKU-BBB");
    }

    [Fact]
    public async Task Include_WithNestedExpandFilter_ComplexChain()
    {
        var parameters = new FlexQueryParameters
        {
            Filter = "Id:eq:1",
            Include = "Orders,Orders.OrderItems",
            Expand = "Orders(filter=Total:gt:100,OrderItems(filter=Sku:eq:SKU-BBB))",
            Select = "Id,Orders.Number,Orders.OrderItems.Sku"
        };

        var result = await _db.Customers
            .AsNoTracking()
            .FlexQueryAsync(parameters);

        var alice = result.Data.First();
        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        var orderList = new List<object>();
        foreach (var o in orders!) orderList.Add(o);
        
        orderList.Should().HaveCount(1);
        var so001 = orderList[0];
        
        var items = so001.GetType().GetProperty("OrderItems")?.GetValue(so001) as System.Collections.IEnumerable;
        var itemList = new List<object>();
        foreach (var i in items!) itemList.Add(i);
        
        itemList.Should().HaveCount(1);
        itemList[0].GetType().GetProperty("Sku")!.GetValue(itemList[0]).Should().Be("SKU-BBB");
    }

    [Fact]
    public async Task Include_WithExpandSort_AppliesSortConfig()
    {
        var parameters = new FlexQueryParameters
        {
            Include = "Orders",
            Expand = "Orders(sort=OrderDate:desc)"
        };

        var result = await _db.Customers
            .AsNoTracking()
            .FlexQueryAsync(parameters);

        result.TotalCount.Should().Be(10);
        var alice = result.Data.First(c => (int)c.GetType().GetProperty("Id")!.GetValue(c)! == 1);
        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        orders.Should().NotBeNull();
        var orderList = new List<object>();
        foreach (var o in orders!) orderList.Add(o);
        orderList.Should().HaveCount(2);
    }

    [Fact]
    public async Task Include_MultipleRoots_WithSelectiveExpandConfig()
    {
        var parameters = new FlexQueryParameters
        {
            Include = "Orders,Profile",
            Expand = "Orders(filter=Total:gt:100)"
        };

        var result = await _db.Customers
            .AsNoTracking()
            .FlexQueryAsync(parameters);

        result.TotalCount.Should().Be(10);
        var alice = result.Data.First(c => (int)c.GetType().GetProperty("Id")!.GetValue(c)! == 1);
        
        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        orders.Should().NotBeNull();
        var orderList = new List<object>();
        foreach (var o in orders!) orderList.Add(o);
        orderList.Should().HaveCount(1);
        
        var profile = alice.GetType().GetProperty("Profile")?.GetValue(alice);
        profile.Should().NotBeNull();
    }

    [Fact]
    public async Task Include_NestedPath_WithNestedExpandConfig()
    {
        var parameters = new FlexQueryParameters
        {
            Filter = "Id:eq:1",
            Include = "Orders,Orders.OrderItems",
            Expand = "Orders(filter=Total:gt:100,OrderItems(filter=Sku:eq:SKU-BBB))"
        };

        var result = await _db.Customers
            .AsNoTracking()
            .FlexQueryAsync(parameters);

        result.TotalCount.Should().Be(1);
        var alice = result.Data.First();
        
        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        orders.Should().NotBeNull();
        var orderList = new List<object>();
        foreach (var o in orders!) orderList.Add(o);
        orderList.Should().HaveCount(1);

        var so001 = orderList[0];
        var items = so001.GetType().GetProperty("OrderItems")?.GetValue(so001) as System.Collections.IEnumerable;
        items.Should().NotBeNull();
        var itemList = new List<object>();
        foreach (var i in items!) itemList.Add(i);
        itemList.Should().HaveCount(1);
        itemList[0].GetType().GetProperty("Sku")!.GetValue(itemList[0]).Should().Be("SKU-BBB");
    }

    [Fact]
    public async Task Legacy_StringIncludes_StillWork()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues>
        {
            ["include"] = "Orders"
        });

        var result = await _db.Customers
            .AsNoTracking()
            .FlexQueryAsync(options);

        result.TotalCount.Should().Be(10);
        var alice = result.Data.First(c => (int)c.GetType().GetProperty("Id")!.GetValue(c)! == 1);
        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        orders.Should().NotBeNull();
        var orderList = new List<object>();
        foreach (var o in orders!) orderList.Add(o);
        orderList.Should().HaveCount(2);
    }
}
