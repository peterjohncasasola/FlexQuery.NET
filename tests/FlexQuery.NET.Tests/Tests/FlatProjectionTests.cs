using FlexQuery.NET;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Tests.Tests;

public class FlatProjectionTests : IDisposable
{
    private readonly TestDbContext _db = TestDbContext.CreateSeeded();
    public void Dispose() => _db.Dispose();

    // ── Flat mode ────────────────────────────────────────────────────────

    [Fact]
    public async Task FlatMode_FlattensSingleLevelCollection_WithAlias()
    {
        var options = new QueryOptions
        {
            ProjectionMode = ProjectionMode.Flat,
            Select = ["Orders.Total", "Orders.Status as OrderStatus"]
        };

        var list = await _db.Entities.Where(x => x.Id == 1).ApplySelect(options).ToListAsync();

        // Entity Id=1 has 2 orders
        list.Should().HaveCount(2);

        var first = list.First();
        var type = first.GetType();

        // 'Total' has no alias → should be 'Total'
        type.GetProperty("Total").Should().NotBeNull();
        // 'Status as OrderStatus' → should be 'OrderStatus' not 'Status'
        type.GetProperty("OrderStatus").Should().NotBeNull();
        type.GetProperty("Status").Should().BeNull("original name should be hidden by alias");

        var total = (decimal)type.GetProperty("Total")!.GetValue(first)!;
        total.Should().Be(50.0m);

        var status = (string)type.GetProperty("OrderStatus")!.GetValue(first)!;
        status.Should().Be("Shipped");
    }

    [Fact]
    public async Task FlatMode_DeepNestedCollection_ProjectsLeafFieldsWithAlias()
    {
        var options = new QueryOptions
        {
            ProjectionMode = ProjectionMode.Flat,
            Select = ["Orders.OrderItems.Quantity as Qty", "Orders.OrderItems.Price"]
        };

        var list = await _db.Entities.Where(x => x.Id == 1).ApplySelect(options).ToListAsync();

        // Order 101 has 2 items, Order 102 has 1 item → 3 total
        list.Should().HaveCount(3);

        var first = list.First();
        var type = first.GetType();

        type.GetProperty("Qty").Should().NotBeNull();
        type.GetProperty("Quantity").Should().BeNull("alias should replace original name");
        type.GetProperty("Price").Should().NotBeNull();

        var qty = (int)type.GetProperty("Qty")!.GetValue(first)!;
        qty.Should().Be(2);

        var price = (decimal)type.GetProperty("Price")!.GetValue(first)!;
        price.Should().Be(25.0m);
    }

    [Fact]
    public void FlatMode_BranchingNavigation_ThrowsInvalidOperationException()
    {
        var options = new QueryOptions
        {
            ProjectionMode = ProjectionMode.Flat,
            Select = ["Orders.Total", "Profile.Bio"]
        };

        Action action = () => _db.Entities.ApplySelect(options);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Flat mode does not support branching multiple navigation paths*");
    }

    [Fact]
    public void FlatMode_MixingRootScalarsWithCollections_ThrowsInvalidOperationException()
    {
        var options = new QueryOptions
        {
            ProjectionMode = ProjectionMode.Flat,
            Select = ["Name", "Orders.Total"]
        };

        Action action = () => _db.Entities.ApplySelect(options);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Flat mode does not support mixing scalar properties*");
    }

    // ── FlatMixed mode ───────────────────────────────────────────────────

    [Fact]
    public async Task FlatMixedMode_CombinesRootScalarsWithDeepCollectionFields()
    {
        var options = new QueryOptions
        {
            ProjectionMode = ProjectionMode.FlatMixed,
            Select = [
                "Name as customerName",
                "Orders.Status as OrderStatus",
                "Orders.Total"
            ]
        };

        var list = await _db.Entities.Where(x => x.Id == 1).ApplySelect(options).ToListAsync();

        // Entity Id=1 has 2 orders → 2 flat rows
        list.Should().HaveCount(2);

        var first = list.First();
        var type = first.GetType();

        // All fields must appear flattened in one object
        type.GetProperty("customerName").Should().NotBeNull();
        type.GetProperty("OrderStatus").Should().NotBeNull();
        type.GetProperty("Total").Should().NotBeNull();

        var customerName = (string)type.GetProperty("customerName")!.GetValue(first)!;
        customerName.Should().Be("Alice Johnson");

        var orderStatus = (string)type.GetProperty("OrderStatus")!.GetValue(first)!;
        orderStatus.Should().BeOneOf("Shipped", "Pending");
    }

    [Fact]
    public async Task FlatMixedMode_MultiLevel_RootPlusIntermediatePlusLeafFields()
    {
        // Simulates: select=id as customerId, orders.status as orderStatus, orders.orderItems.quantity as qty
        var options = new QueryOptions
        {
            ProjectionMode = ProjectionMode.FlatMixed,
            Select = [
                "Id as customerId",
                "Orders.Status as orderStatus",
                "Orders.OrderItems.Quantity as qty"
            ]
        };

        var list = await _db.Entities.Where(x => x.Id == 1).ApplySelect(options).ToListAsync();

        // Entity Id=1: Order 101 (Shipped) has 2 items, Order 102 (Pending) has 1 item
        // → 3 flat rows total
        list.Should().HaveCount(3);

        var first = list.First();
        var type = first.GetType();

        type.GetProperty("customerId").Should().NotBeNull("root field with alias");
        type.GetProperty("orderStatus").Should().NotBeNull("intermediate nav field with alias");
        type.GetProperty("qty").Should().NotBeNull("leaf field with alias");

        // All rows belong to Customer 1
        var customerId = (int)type.GetProperty("customerId")!.GetValue(first)!;
        customerId.Should().Be(1);

        // Order status should come from the intermediate level
        var orderStatus = (string)type.GetProperty("orderStatus")!.GetValue(first)!;
        orderStatus.Should().BeOneOf("Shipped", "Pending");

        // Qty from the leaf OrderItem
        var qty = (int)type.GetProperty("qty")!.GetValue(first)!;
        qty.Should().BeGreaterThan(0);
    }

    // ── Nested mode alias ────────────────────────────────────────────────

    [Fact]
    public async Task NestedMode_AliasOnScalarField_ReplacesPropertyNameInOutput()
    {
        var options = new QueryOptions
        {
            Select = ["Id as customerId", "Name"]
        };

        var list = await _db.Entities.Where(x => x.Id == 1).ApplySelect(options).ToListAsync();

        list.Should().HaveCount(1);
        var first = list.First();
        var type = first.GetType();

        type.GetProperty("customerId").Should().NotBeNull("alias must appear as output name");
        type.GetProperty("Id").Should().BeNull("original name should be hidden by alias");
        type.GetProperty("Name").Should().NotBeNull();

        var id = (int)type.GetProperty("customerId")!.GetValue(first)!;
        id.Should().Be(1);
    }

    [Fact]
    public async Task NestedMode_AliasOnNestedScalar_AppliedAtLeafLevel()
    {
        var options = new QueryOptions
        {
            Select = ["Id", "Orders.Status as OrderStatus", "Orders.Total"]
        };

        var list = await _db.Entities.Where(x => x.Id == 1).ApplySelect(options).ToListAsync();

        list.Should().HaveCount(1);
        var first = list.First();
        var type = first.GetType();

        type.GetProperty("Id").Should().NotBeNull();
        var ordersProp = type.GetProperty("Orders");
        ordersProp.Should().NotBeNull();

        var orders = ((System.Collections.IEnumerable)ordersProp!.GetValue(first)!).Cast<object>().ToList();
        orders.Should().HaveCount(2);

        var firstOrder = orders.First();
        var orderType = firstOrder.GetType();

        orderType.GetProperty("OrderStatus").Should().NotBeNull("alias on nested scalar must be applied");
        orderType.GetProperty("Status").Should().BeNull("original name hidden by alias");
        orderType.GetProperty("Total").Should().NotBeNull();

        var status = (string)orderType.GetProperty("OrderStatus")!.GetValue(firstOrder)!;
        status.Should().Be("Shipped");
    }
}
