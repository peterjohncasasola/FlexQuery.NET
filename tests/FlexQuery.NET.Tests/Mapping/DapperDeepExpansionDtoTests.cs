using System.Data;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Mapping;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FlexQuery.NET.Tests.Mapping;

/// <summary>
/// Deep collection expansion regression tests (Dapper provider, typed DTO path):
/// <c>include=Orders,Orders.OrderItems</c> + <c>expand=Orders(...),Orders.OrderItems(...)</c>
/// must hydrate the nested DTO graph (Customer → Orders → OrderItems) with per-parent
/// takes, filters, and ordering applied at the database level.
/// Mappings are registered globally (FlexQueryMapping), matching the documented
/// AddFlexQuery startup pattern.
/// </summary>
[Collection("GlobalMapping")]
public class DapperDeepExpansionDtoTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public DapperDeepExpansionDtoTests()
    {
        FlexQueryMapping.Reset();
        FlexQueryMapping.Configure(registry =>
        {
            RegisterCustomerMap(registry);
            RegisterOrderMap(registry);
            RegisterOrderItemMap(registry);
        });

        var ctx = SharedTestDbContext.CreateSqlite();
        SampleData.Seed(ctx);
        _connection = (SqliteConnection)ctx.Database.GetDbConnection();
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }

    /// <summary>
    /// Registers the documented global mapping graph with renamed scalar members
    /// (CustomerId ← Id, CustomerFullName ← Name, OrderId ← Id, OrderItemId ← Id).
    /// Same-name members (Status, Quantity) and same-name collections (Orders,
    /// OrderItems, resolved element-wise) map by convention.
    /// </summary>
    private static void RegisterRenamed<TSource, TDestination>(
        IQueryMappingRegistry registry,
        params (string Destination, System.Linq.Expressions.LambdaExpression Source, System.Reflection.PropertyInfo SourceProperty, System.Reflection.PropertyInfo DestinationProperty)[] members)
    {
        var typeMap = (TypeMap)registry.GetOrCreate(typeof(TSource), typeof(TDestination));
        foreach (var (destination, source, sourceProperty, destinationProperty) in members)
        {
            typeMap.RegisterMember(new PropertyMap
            {
                DestinationName = destination,
                SourceExpression = source,
                SourceProperty = sourceProperty,
                DestinationProperty = destinationProperty,
                SourceValueType = source.ReturnType,
                DestinationValueType = destinationProperty.PropertyType,
                IsImplicit = false,
                IsNavigation = !FlexQuery.NET.Metadata.TypeClassification.IsScalarType(source.ReturnType)
            });
        }
    }

    private static void RegisterCustomerMap(IQueryMappingRegistry registry)
        => RegisterRenamed<Customer, CustomerResponse>(
            registry,
            (nameof(CustomerResponse.CustomerId),
                (System.Linq.Expressions.Expression<Func<Customer, int>>)(e => e.Id),
                typeof(Customer).GetProperty(nameof(Customer.Id))!,
                typeof(CustomerResponse).GetProperty(nameof(CustomerResponse.CustomerId))!),
            (nameof(CustomerResponse.CustomerFullName),
                (System.Linq.Expressions.Expression<Func<Customer, string>>)(e => e.Name),
                typeof(Customer).GetProperty(nameof(Customer.Name))!,
                typeof(CustomerResponse).GetProperty(nameof(CustomerResponse.CustomerFullName))!));

    private static void RegisterOrderMap(IQueryMappingRegistry registry)
        => RegisterRenamed<Order, OrderResponse>(
            registry,
            (nameof(OrderResponse.OrderId),
                (System.Linq.Expressions.Expression<Func<Order, int>>)(e => e.Id),
                typeof(Order).GetProperty(nameof(Order.Id))!,
                typeof(OrderResponse).GetProperty(nameof(OrderResponse.OrderId))!));

    private static void RegisterOrderItemMap(IQueryMappingRegistry registry)
        => RegisterRenamed<OrderItem, OrderItemResponse>(
            registry,
            (nameof(OrderItemResponse.OrderItemId),
                (System.Linq.Expressions.Expression<Func<OrderItem, int>>)(e => e.Id),
                typeof(OrderItem).GetProperty(nameof(OrderItem.Id))!,
                typeof(OrderItemResponse).GetProperty(nameof(OrderItemResponse.OrderItemId))!));

    public void Dispose()
    {
        FlexQueryMapping.Reset();
        _connection.Dispose();
    }

    // DTO graph mirroring the documented response contract ---------------------------

    public class CustomerResponse
    {
        public int CustomerId { get; set; }
        public string CustomerFullName { get; set; } = string.Empty;
        public List<OrderResponse> Orders { get; set; } = [];
    }

    public class OrderResponse
    {
        public int OrderId { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<OrderItemResponse> OrderItems { get; set; } = [];
    }

    public class OrderItemResponse
    {
        public int OrderItemId { get; set; }
        public int Quantity { get; set; }
    }

    // Seed reference:
    //   Alice (1): order 10001 Shipped [item 1 qty 2, item 2 qty 1], order 10002 Pending [item 3 qty 3]
    //   Bob   (2): order 10003 Delivered (no items)
    //   Carol (3): order 10004 Cancelled (no items)

    // Test 1 — exact failing request (delivered orders) --------------------------------

    [Fact]
    public async Task ExactRepro_DeliveredOrders_CustomersAndOrders()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Filter = "CustomerId:eq:2",
                Include = "Orders,Orders.OrderItems",
                Expand = "Orders(take=1; filter=Status:eq:Delivered; sort=Id:desc),Orders.OrderItems(take=1)",
                PageSize = 5
            });

        // Bob (Id 2) has the only Delivered order (10003) — with no OrderItems seeded.
        result.Data.Should().ContainSingle();
        var orders = result.Data[0].Orders;
        orders.Should().ContainSingle();
        orders[0].OrderId.Should().Be(10003);
        orders[0].Status.Should().Be("Delivered");
        orders[0].OrderItems.Should().BeEmpty("order 10003 has no matching OrderItems — empty is correct here");
    }

    [Fact]
    public async Task ShippedOrders_PopulatedOrderItems_WhenRowsExist()
    {
        // Alice has order 10001 (Shipped) with items 1 and 2.
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Filter = "CustomerId:eq:1",
                Include = "Orders,Orders.OrderItems",
                Expand = "Orders(take=1; filter=Status:eq:Shipped; sort=Id:desc),Orders.OrderItems(take=1)",
                PageSize = 5
            });

        result.Data.Should().ContainSingle();
        var orders = result.Data[0].Orders;
        orders.Should().ContainSingle();
        orders[0].OrderId.Should().Be(10001);
        orders[0].Status.Should().Be("Shipped");
        orders[0].OrderItems.Should().ContainSingle("OrderItems.Take(1) per selected Order");
        orders[0].OrderItems[0].OrderItemId.Should().Be(1);
        orders[0].OrderItems[0].Quantity.Should().Be(2);
    }

    // Test 2 — child Take per parent ---------------------------------------------------

    [Fact]
    public async Task ChildTake_AppliesPerParent()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Filter = "CustomerId:eq:1",
                Include = "Orders,Orders.OrderItems",
                Expand = "Orders(take=2),Orders.OrderItems(take=1)",
                PageSize = 5
            });

        var orders = result.Data[0].Orders;
        orders.Should().HaveCount(2);
        foreach (var order in orders)
        {
            order.OrderItems.Should().ContainSingle($"each order (Id={order.OrderId}) gets its own Take(1)");
        }
        // Distinct items per order — not one shared item.
        orders[0].OrderItems[0].OrderItemId.Should().NotBe(orders[1].OrderItems[0].OrderItemId);
    }

    // Test 3 — child ordering ------------------------------------------------------------

    [Fact]
    public async Task ChildOrdering_Desc_ReturnsFirstPerOrder()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Filter = "CustomerId:eq:1",
                Include = "Orders,Orders.OrderItems",
                Expand = "Orders(take=2),Orders.OrderItems(take=1; sort=Id:desc)",
                PageSize = 5
            });

        var orders = result.Data[0].Orders;
        orders.Should().HaveCount(2);
        // Order 10001 items: [1, 2] → desc first = 2. Order 10002 items: [3] → 3.
        orders.First(o => o.OrderId == 10001).OrderItems[0].OrderItemId.Should().Be(2);
        orders.First(o => o.OrderId == 10002).OrderItems[0].OrderItemId.Should().Be(3);
    }

    // Test 4 — parent filtering excludes child rows ---------------------------------------

    [Fact]
    public async Task ParentFilter_ExcludedOrders_ChildRowsNeverAppear()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Filter = "CustomerId:eq:1",
                Include = "Orders,Orders.OrderItems",
                Expand = "Orders(filter=Status:eq:Shipped;take=3),Orders.OrderItems(take=1)",
                PageSize = 5
            });

        var orders = result.Data[0].Orders;
        orders.Should().NotBeEmpty();
        orders.All(o => o.Status == "Shipped").Should().BeTrue();
        // Pending order 10002's items never appear.
        orders.SelectMany(o => o.OrderItems).Should().OnlyContain(i => i.OrderItemId == 1 || i.OrderItemId == 2);
    }

    // Test 5 — no matching child rows → empty collection ---------------------------------

    [Fact]
    public async Task NoMatchingChildRows_EmptyCollection()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Filter = "CustomerId:eq:2",
                Include = "Orders,Orders.OrderItems",
                Expand = "Orders(take=3),Orders.OrderItems(take=5)",
                PageSize = 5
            });

        var orders = result.Data[0].Orders;
        orders.Should().ContainSingle();
        orders[0].OrderItems.Should().BeEmpty("order 10003 has no OrderItems in the database");
    }

    // Test 6 — missing deep include fails validation ---------------------------------------

    [Fact]
    public async Task MissingDeepInclude_FailsValidation()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _connection.FlexQueryAsync<Customer, CustomerResponse>(
                new FlexQueryParameters
                {
                    Filter = "CustomerId:eq:1",
                    Include = "Orders",
                    Expand = "Orders.OrderItems(take=1)",
                    PageSize = 5
                }));

        ex.Message.Should().Contain("Orders.OrderItems");
    }

    // Test 7 — nested DTO graph materialization with renamed members -----------------------

    [Fact]
    public async Task NestedDtoGraph_RenamedMembers_Materialize()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Filter = "CustomerId:eq:1",
                Include = "Orders,Orders.OrderItems",
                Expand = "Orders(take=2; sort=Id:desc),Orders.OrderItems(take=5)",
                PageSize = 5
            });

        var customer = result.Data[0];
        customer.CustomerId.Should().Be(1);
        customer.CustomerFullName.Should().Be("Alice Johnson");

        customer.Orders.Should().HaveCount(2);
        foreach (var order in customer.Orders)
        {
            order.OrderId.Should().BeGreaterThan(0);
            order.Status.Should().NotBeNullOrEmpty();
            order.OrderItems.Should().NotBeNull();
        }

        var ids = customer.Orders.Select(o => o.OrderId).ToList();
        ids.Should().BeInDescendingOrder();
    }
}
