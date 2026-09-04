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

namespace FlexQuery.NET.Tests.Dapper.Integration;

/// <summary>
/// Regression tests for Dapper DTO nested collection expansion
/// (<c>expand=Orders(...),Orders.OrderItems(...)</c> with flat dotted paths).
///
/// The failing request was:
/// <code>
/// GET /customers-dto/dapper?include=Orders,Orders.OrderItems
///     &amp;expand=Orders(take=1;filter=Status="delivered";sort=OrderId DESC),Orders.OrderItems(take=1)
///     &amp;page=1&amp;pageSize=5
/// </code>
/// Orders hydrated but every <c>orderItems</c> was <c>[]</c>. These tests pin the
/// per-parent child take, child ordering, parent filtering, empty-but-valid
/// collections, exact-match include validation, DTO graph materialization, and
/// deeper nesting through the public (unchanged) syntax.
/// </summary>
[Collection("GlobalMapping")]
public class DapperDtoNestedExpansionTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public DapperDtoNestedExpansionTests()
    {
        global::FlexQuery.NET.Parsers.Fql.Fql.Register();

        FlexQueryMapping.Reset();
        FlexQueryMapping.Configure(registry =>
        {
            registry.GetOrCreate<Customer, CustomerResponse>()
                ; // same-name scalars by convention (Id, Name, ...)
            RegisterMap<Customer, CustomerResponse>(registry);
            RegisterMap<Order, OrderResponse>(registry);
            RegisterMap<OrderItem, OrderItemResponse>(registry);
            RegisterMap<Discount, DiscountResponse>(registry);
            RegisterMap<Customer, CustomerDtoResponse>(registry);
        });

        var ctx = SharedTestDbContext.CreateSqlite();
        SampleData.Seed(ctx);
        _connection = (SqliteConnection)ctx.Database.GetDbConnection();
        if (_connection.State != ConnectionState.Open) _connection.Open();

        // Additional fixture rows (idempotent seed base is empty of these ids):
        //   Customer 2: Delivered order 10005 with items 10, 11 (desc-first = 11).
        //   Customer 3: Delivered order 10006 with NO items (empty collection case).
        //   Customer 4: Delivered order 10007 with EIGHT items 20..27 (per-parent take proof).
        //   Order 10005 items 10, 11 each carry two discounts (third-level materialization).
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = OFF;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = """
            INSERT INTO Customers (Id, Name, Email, Phone, Age, City, Status, IsActive, CreatedAt, SSN, Salary, Category, SecretField, Country) VALUES
                (11, 'Zoe Grant', 'zoe@example.com', NULL, 40, 'Berlin', 'Active', 0, '2023-11-01', NULL, 50000, '', '', '');
            INSERT INTO Orders (Id, CustomerId, OrderDate, Status, Total, Price, Category, Number) VALUES
                (10005, 2, '2023-02-10', 'Delivered', 60, 0, '', 'SO-005'),
                (10006, 3, '2023-03-10', 'Delivered', 70, 0, '', 'SO-006'),
                (10007, 4, '2023-04-10', 'Delivered', 80, 0, '', 'SO-007'),
                (10008, 11, '2023-05-10', 'Delivered', 90, 0, '', 'SO-008'),
                (10009, 11, '2023-06-10', 'Delivered', 95, 0, '', 'SO-009');
            INSERT INTO OrderItems (Id, OrderId, ProductId, Quantity, Price, Sku, UnitPrice) VALUES
                (10, 10005, 1, 4, 30, 'SKU-EEE', 30),
                (11, 10005, 1, 2, 30, 'SKU-FFF', 30),
                (20, 10007, 1, 1, 10, 'SKU-P20', 10),
                (21, 10007, 1, 1, 10, 'SKU-P21', 10),
                (22, 10007, 1, 1, 10, 'SKU-P22', 10),
                (23, 10007, 1, 1, 10, 'SKU-P23', 10),
                (24, 10007, 1, 1, 10, 'SKU-P24', 10),
                (25, 10007, 1, 1, 10, 'SKU-P25', 10),
                (26, 10007, 1, 1, 10, 'SKU-P26', 10),
                (27, 10007, 1, 1, 10, 'SKU-P27', 10),
                (30, 10008, 1, 1, 10, 'SKU-Z30', 10),
                (31, 10008, 1, 1, 10, 'SKU-Z31', 10),
                (32, 10008, 1, 1, 10, 'SKU-Z32', 10),
                (33, 10008, 1, 1, 10, 'SKU-Z33', 10),
                (34, 10008, 1, 1, 10, 'SKU-Z34', 10),
                (35, 10008, 1, 1, 10, 'SKU-Z35', 10),
                (36, 10008, 1, 1, 10, 'SKU-Z36', 10),
                (40, 10009, 1, 1, 10, 'SKU-Y40', 10),
                (41, 10009, 1, 1, 10, 'SKU-Y41', 10),
                (42, 10009, 1, 1, 10, 'SKU-Y42', 10),
                (43, 10009, 1, 1, 10, 'SKU-Y43', 10),
                (44, 10009, 1, 1, 10, 'SKU-Y44', 10),
                (45, 10009, 1, 1, 10, 'SKU-Y45', 10);
            INSERT INTO Discounts (Id, OrderItemId, Code, Rate) VALUES
                (1, 10, 'D-TEN', 0.1),
                (2, 10, 'D-FIF', 0.15),
                (3, 11, 'D-TEN', 0.1),
                (4, 11, 'D-FIF', 0.15);
            """;
        cmd.ExecuteNonQuery();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        FlexQueryMapping.Reset();
        _connection.Dispose();
    }

    // DTO graph (documented response contract) ---------------------------------------

    public class OrderItemResponse
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
        public List<DiscountResponse> Discounts { get; set; } = [];
    }

    public class DiscountResponse
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    public class OrderResponse
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<OrderItemResponse> OrderItems { get; set; } = [];
    }

    public class CustomerResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<OrderResponse> Orders { get; set; } = [];
    }

    public class CustomerDtoResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<OrderResponse> Orders { get; set; } = [];
    }

    /// <summary>
    /// Registers no explicit members — the same-name convention (scalar type equality,
    /// element-wise collection compatibility with a registered nested map) covers this
    /// graph. Kept as a seam for graph variants.
    /// </summary>
    private static void RegisterMap<TEntity, TResponse>(IQueryMappingRegistry registry)
        where TEntity : class
        where TResponse : class
        => registry.GetOrCreate<TEntity, TResponse>();

    private static Action<DapperQueryOptions> Fql => opt =>
        opt.QuerySyntax = global::FlexQuery.NET.Parsers.QuerySyntax.Fql;

    private static object? Prop(object instance, string name)
        => instance.GetType().GetProperty(name,
               System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
           ?.GetValue(instance);

    // Seed reference (SampleData.Seed):
    //   Alice (1): order 10001 Shipped [item 1 qty 2, item 2 qty 1], order 10002 Pending [item 3 qty 3]
    //   Bob   (2): order 10003 Delivered (no items) [+ seeded 10005 Delivered: items 10, 11]
    //   Carol (3): order 10004 Cancelled (no items) [+ seeded 10006 Delivered: no items]
    //   David (4): [seeded 10007 Delivered: items 20..27]
    //   Zoe  (11): [seeded 10008 Delivered: items 30..36, 10009 Delivered: items 40..45]
    //   Item 10 → discounts 1, 2. Item 11 → discounts 3, 4.

    // Test 1 — the exact failing request shape (continuation: child take = 5) ----------

    [Fact]
    public async Task ExactRequest_Take5_PerOrder_WithFilteredSortedParentWindow()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Include = "Orders,Orders.OrderItems",
                Expand = "Orders(take=1;filter=Status=\"Delivered\";sort=Id DESC),Orders.OrderItems(take=5)",
                Page = 1,
                PageSize = 5
            },
            Fql);

        // Root pagination applies to Customers only: a full page of 5.
        result.Data.Should().HaveCount(5);

        foreach (var customer in result.Data)
        {
            // Parent window: at most 1 Delivered order per customer, ordered by Id DESC.
            customer.Orders.Should().HaveCountLessThanOrEqualTo(1);
            foreach (var order in customer.Orders)
            {
                order.Status.Should().Be("Delivered");

                // Child window: at most 5 items per selected order — populated whenever
                // matching rows exist (the regression), empty otherwise.
                order.OrderItems.Should().HaveCountLessThanOrEqualTo(5);
            }
        }

        // Customer 4: only Delivered order is 10007 with EIGHT items → exactly the
        // first five materialize, ordered by the default window order (Id ASC).
        var david = result.Data.Single(c => c.Id == 4);
        var davidOrder = david.Orders.Should().ContainSingle().Subject;
        davidOrder.Id.Should().Be(10007);
        davidOrder.OrderItems.Select(i => i.Id).Should().Equal([20, 21, 22, 23, 24]);

        // Customer 2: order 10005 has 2 items → both materialize under take=5.
        var bob = result.Data.Single(c => c.Id == 2);
        var bobOrder = bob.Orders.Should().ContainSingle().Subject;
        bobOrder.Id.Should().Be(10005);
        bobOrder.OrderItems.Select(i => i.Id).Should().Equal([10, 11]);

        // Customer 3: order 10006 has no items → empty collection is correct.
        var carol = result.Data.Single(c => c.Id == 3);
        carol.Orders.Should().ContainSingle().Which.OrderItems.Should().BeEmpty();
    }

    // Test 2 — equivalent requests through FlexQueryAsync<TEntity> and
    // FlexQueryAsync<TEntity, TDto> must produce the same navigation expansion
    // semantics: same roots, same selected orders, same items per order. The DTO
    // path is allowed to change element types, never the expanded graph.

    [Fact]
    public async Task DtoAndNonDto_EquivalentRequests_ProduceSameNavigationExpansion()
    {
        var parameters = new FlexQueryParameters
        {
            Include = "Orders,Orders.OrderItems",
            Expand = "Orders(take=1;filter=Status=\"Delivered\";sort=Id DESC),Orders.OrderItems(take=5)",
            Page = 1,
            PageSize = 5
        };

        var nonDto = await _connection.FlexQueryAsync<Customer>(parameters, Fql);
        var dto = await _connection.FlexQueryAsync<Customer, CustomerResponse>(parameters, Fql);

        nonDto.Data.Should().HaveCount(dto.Data.Count);

        for (var i = 0; i < nonDto.Data.Count; i++)
        {
            var dtoCustomer = dto.Data[i];

            var entityOrders = ((System.Collections.IEnumerable)Prop(nonDto.Data[i], "Orders")!)
                .Cast<object>().ToList();

            dtoCustomer.Orders.Select(o => o.Id).Should().Equal(
                entityOrders.Select(o => (int)Prop(o, "Id")!),
                $"customer {dtoCustomer.Id}: the DTO path must select the same orders as the entity path");

            for (var j = 0; j < entityOrders.Count; j++)
            {
                var dtoOrder = dtoCustomer.Orders[j];
                var entityItems = ((System.Collections.IEnumerable)Prop(entityOrders[j], "OrderItems")!)
                    .Cast<object>().ToList();

                dtoOrder.OrderItems.Select(it => it.Id).Should().Equal(
                    entityItems.Select(it => (int)Prop(it, "Id")!),
                    $"order {dtoOrder.Id}: the DTO path must materialize the same items as the entity path");
            }
        }

        // The parity assertion is only meaningful when items actually materialize:
        // Customer 4 → order 10007 → items 20..24 must agree on BOTH paths.
        var david = dto.Data.Single(c => c.Id == 4);
        david.Orders.Single(o => o.Id == 10007).OrderItems.Select(it => it.Id)
            .Should().Equal([20, 21, 22, 23, 24]);
    }

    // Test 3 — child Take applies per parent, not globally -----------------------------

    [Fact]
    public async Task ChildTake_AppliesPerSelectedOrder()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Filter = "Id = 1",
                Include = "Orders,Orders.OrderItems",
                Expand = "Orders(take=2;sort=Id ASC),Orders.OrderItems(take=1)",
                PageSize = 5
            },
            Fql);

        var orders = result.Data[0].Orders;
        orders.Should().HaveCount(2);
        foreach (var order in orders)
            order.OrderItems.Should().ContainSingle($"order {order.Id} gets its own Take(1)");

        // Distinct child rows per parent — not one shared item across all orders.
        orders[0].OrderItems[0].Id.Should().NotBe(orders[1].OrderItems[0].Id);
    }

    [Fact]
    public async Task ChildTake5_WithMoreThanFiveItemsPerOrder_AppliesPerParent()
    {
        // Zoe (11) has two Delivered orders, each with SEVEN items. A global take
        // would yield 5 items total; per-parent takes yield 5 + 5.
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Filter = "Id = 11",
                Include = "Orders,Orders.OrderItems",
                Expand = "Orders(take=2;sort=Id ASC),Orders.OrderItems(take=5)",
                PageSize = 5
            },
            Fql);

        var orders = result.Data.Should().ContainSingle().Subject.Orders;
        orders.Select(o => o.Id).Should().Equal([10008, 10009]);
        orders[0].OrderItems.Should().HaveCount(5, "order 10008 gets its own Take(5)");
        orders[1].OrderItems.Should().HaveCount(5, "order 10009 gets its own Take(5)");
        orders[0].OrderItems.Select(i => i.Id).Should().Equal([30, 31, 32, 33, 34]);
        orders[1].OrderItems.Select(i => i.Id).Should().Equal([40, 41, 42, 43, 44]);
    }

    // Test 3 — child ordering ----------------------------------------------------------

    [Fact]
    public async Task ChildSort_Desc_ReturnsFirstItemPerOrder()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Filter = "Id = 1",
                Include = "Orders,Orders.OrderItems",
                Expand = "Orders(take=2;sort=Id ASC),Orders.OrderItems(take=1;sort=Id DESC)",
                PageSize = 5
            },
            Fql);

        var orders = result.Data[0].Orders;
        orders.Should().HaveCount(2);
        // Order 10001 items: [1, 2] → desc-first = 2. Order 10002 items: [3] → 3.
        orders.First(o => o.Id == 10001).OrderItems[0].Id.Should().Be(2);
        orders.First(o => o.Id == 10002).OrderItems[0].Id.Should().Be(3);

        var bob = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Filter = "Id = 2",
                Include = "Orders,Orders.OrderItems",
                Expand = "Orders(take=1;sort=Id DESC),Orders.OrderItems(take=1;sort=Id DESC)",
                PageSize = 5
            },
            Fql);
        // Order 10005 items: [10, 11] → desc-first = 11.
        bob.Data[0].Orders[0].OrderItems[0].Id.Should().Be(11);
    }

    // Test 4 — parent filtering excludes child rows --------------------------------------

    [Fact]
    public async Task ParentFilter_ExcludedOrders_NeverLeakChildRows()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Filter = "Id = 1",
                Include = "Orders,Orders.OrderItems",
                Expand = "Orders(filter=Status=\"Shipped\";take=3),Orders.OrderItems(take=5)",
                PageSize = 5
            },
            Fql);

        var orders = result.Data[0].Orders;
        orders.Should().NotBeEmpty();
        orders.All(o => o.Status == "Shipped").Should().BeTrue();

        // Pending order 10002's item (3) must never appear under a Shipped order.
        orders.SelectMany(o => o.OrderItems).Should().OnlyContain(i => i.Id == 1 || i.Id == 2);
    }

    // Test 5 — no matching child rows → empty collection (not null, not dropped) ---------

    [Fact]
    public async Task OrderWithoutItems_EmptyCollection_WhilePopulatedOrdersStayPopulated()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Filter = "Id = 2",
                Include = "Orders,Orders.OrderItems",
                Expand = "Orders(take=5;sort=Id ASC),Orders.OrderItems(take=5)",
                PageSize = 5
            },
            Fql);

        var orders = result.Data[0].Orders;
        orders.Select(o => o.Id).Should().Equal([10003, 10005]);
        orders.First(o => o.Id == 10003).OrderItems.Should().BeEmpty("order 10003 has no matching rows — empty is correct");
        orders.First(o => o.Id == 10005).OrderItems.Should().NotBeEmpty("order 10005 has matching rows — must not be []");
    }

    // Test 6 — deep expansion without the exact deep include fails validation -------------

    [Fact]
    public async Task DeepExpand_WithoutDeepInclude_FailsValidation()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _connection.FlexQueryAsync<Customer, CustomerResponse>(
                new FlexQueryParameters
                {
                    Filter = "Id = 1",
                    Include = "Orders",
                    Expand = "Orders.OrderItems(take=1)",
                    PageSize = 5
                },
                Fql));

        ex.Message.Should().Contain("Orders.OrderItems");
    }

    // Test 7 — nested DTO graph materialization (renamed-style surface preserved) ---------

    [Fact]
    public async Task NestedDtoGraph_MaterializesCustomerToOrderToItem()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Filter = "Id = 2",
                Include = "Orders,Orders.OrderItems",
                Expand = "Orders(take=1;sort=Id DESC;filter=Status=\"Delivered\"),Orders.OrderItems(take=1;sort=Id DESC)",
                PageSize = 5
            },
            Fql);

        var customer = result.Data.Should().ContainSingle().Subject;
        customer.Id.Should().Be(2);
        customer.Name.Should().Be("Bob Smith");

        var order = customer.Orders.Should().ContainSingle().Subject;
        order.Id.Should().Be(10005);
        order.Status.Should().Be("Delivered");

        var item = order.OrderItems.Should().ContainSingle().Subject;
        item.Id.Should().Be(11);
        item.Quantity.Should().Be(2);
    }

    // Test 7b — ENTITY-TYPED nested collection (host-app shape) --------------------------
    // OrderResponse.OrderItems is List<OrderItem> (the entity type, not a DTO type) and
    // only Customer→Response / Order→Response maps are registered — exactly the
    // FlexQueryDemo host configuration. The split-query projection emits dynamic row
    // shapes; the entity-typed fast path must re-materialize projected elements into the
    // declared entity element type instead of silently dropping the collection.
    // Also pins the host's raw Postman strings (spaces around '=', capital 'Filter',
    // space after the include comma).

    public class HostOrderResponse
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<OrderItem> OrderItems { get; set; } = [];
    }

    public class HostCustomerResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<HostOrderResponse> Orders { get; set; } = [];
    }

    [Fact]
    public async Task EntityTypedNestedCollection_MaterializesUnderCorrectOrder()
    {
        var result = await _connection.FlexQueryAsync<Customer, HostCustomerResponse>(
            new FlexQueryParameters
            {
                Include = "Orders,Orders.OrderItems",
                Expand = "Orders(take=1;filter=Status=\"Delivered\";sort=Id DESC),Orders.OrderItems(take=5)",
                Page = 1,
                PageSize = 5
            },
            Fql);

        var bob = result.Data.Single(c => c.Id == 2);
        var order = bob.Orders.Should().ContainSingle().Subject;
        order.Id.Should().Be(10005);
        order.OrderItems.Select(i => i.Id).Should().Equal([10, 11]);
    }

    [Fact]
    public async Task EntityTypedNestedCollection_HostRawStrings_WithWhitespaceAndCasing()
    {
        // Raw strings from the failing Postman request: spaces around '=', 'Filter' key
        // casing, space after the include comma. The status value keeps the fixture's
        // stored casing — SQLite '=' is case-sensitive (the host's SQL Server is not).
        var result = await _connection.FlexQueryAsync<Customer, HostCustomerResponse>(
            new FlexQueryParameters
            {
                Include = "Orders, Orders.OrderItems",
                Expand = "Orders(take = 1; Filter = status = \"Delivered\"; sort = Id DESC), Orders.OrderItems(take=5)",
                Page = 1,
                PageSize = 5
            },
            Fql);

        var bob = result.Data.Single(c => c.Id == 2);
        var order = bob.Orders.Should().ContainSingle().Subject;
        order.Status.Should().Be("Delivered");
        order.OrderItems.Select(i => i.Id).Should().Equal([10, 11]);
    }

    // Test 8 — select interaction: select stays independent of expand ----------------------

    [Fact]
    public async Task Select_NestedFields_WithDeepExpand_ReturnsSelectedFieldsAndNestedItems()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Filter = "Id = 1",
                Include = "Orders,Orders.OrderItems",
                Select = "Name,Orders(Id,Status,OrderItems(Id,Quantity))",
                Expand = "Orders(take=2;sort=Id ASC),Orders.OrderItems(take=2;sort=Id ASC)",
                PageSize = 5
            },
            Fql);

        var customer = result.Data.Should().ContainSingle().Subject;
        customer.Name.Should().Be("Alice Johnson");

        var orders = customer.Orders;
        orders.Should().HaveCount(2);
        orders.First(o => o.Id == 10001).OrderItems.Select(i => i.Id).Should().Equal([1, 2]);
        orders.First(o => o.Id == 10002).OrderItems.Select(i => i.Id).Should().Equal([3]);
    }

    // Test 9 — third-level collection materialization (recursive, not hard-coded) --------
    // Orders → OrderItems → Discounts through the same flat-path syntax; the split-query
    // hydration must recurse over the normalized expansion tree at arbitrary depth.

    [Fact]
    public async Task ThirdLevelCollection_MaterializesUnderCorrectParent()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Filter = "Id = 2",
                Include = "Orders,Orders.OrderItems,Orders.OrderItems.Discounts",
                Expand = "Orders(take=1;sort=Id DESC),Orders.OrderItems(take=5;sort=Id ASC),Orders.OrderItems.Discounts(take=5;sort=Id ASC)",
                PageSize = 5
            },
            Fql);

        var order = result.Data[0].Orders.Should().ContainSingle().Subject;
        order.Id.Should().Be(10005);

        var item10 = order.OrderItems.First(i => i.Id == 10);
        var item11 = order.OrderItems.First(i => i.Id == 11);

        // Discounts correlate to their own OrderItem — no cross-parent contamination.
        item10.Discounts.Select(d => d.Id).Should().Equal([1, 2]);
        item11.Discounts.Select(d => d.Id).Should().Equal([3, 4]);
    }

    [Fact]
    public async Task ThirdLevelTake_AppliesPerParentItem()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            new FlexQueryParameters
            {
                Filter = "Id = 2",
                Include = "Orders,Orders.OrderItems,Orders.OrderItems.Discounts",
                Expand = "Orders(take=1;sort=Id DESC),Orders.OrderItems(take=5;sort=Id ASC),Orders.OrderItems.Discounts(take=1;sort=Id DESC)",
                PageSize = 5
            },
            Fql);

        var order = result.Data[0].Orders.Should().ContainSingle().Subject;

        // Each item gets its own Take(1) of discounts, ordered DESC.
        order.OrderItems.First(i => i.Id == 10).Discounts.Select(d => d.Id).Should().Equal([2]);
        order.OrderItems.First(i => i.Id == 11).Discounts.Select(d => d.Id).Should().Equal([4]);
    }
}
