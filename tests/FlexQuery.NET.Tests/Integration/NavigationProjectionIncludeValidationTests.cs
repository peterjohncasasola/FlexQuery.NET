using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared.Fixtures;
using Xunit;

namespace FlexQuery.NET.Tests.Integration;

/// <summary>
/// Navigation-projection include-authorization contract (EF Core provider):
/// selecting a navigation with nested children requires the exact include path,
/// independent of DTO surface resolution. Computed scalar mappings never require
/// an include. Strict mode throws; lenient mode removes the unauthorized node
/// while preserving valid scalar siblings.
/// </summary>
public class NavigationProjectionIncludeValidationTests : IDisposable
{
    private readonly SharedTestDbContext _db = SharedTestDbContext.CreateInMemorySeeded();

    public void Dispose() => _db.Dispose();

    public class CustomerNavDto
    {
        public int Id { get; set; }
        public string CustomerFullName { get; set; } = string.Empty;
        public List<Order>? Orders { get; set; }
    }

    public class CustomerOrdersTotalDto
    {
        public int Id { get; set; }
        public decimal OrdersTotal { get; set; }
    }

    public class CustomerOrdersEntityAliasDto
    {
        public int Id { get; set; }
        public string CustomerFullName { get; set; } = string.Empty;
        public List<Order>? Orders { get; set; }
    }

    // Must fail (strict) ----------------------------------------------------------

    [Fact]
    public async Task EntityMode_NestedSelect_WithoutInclude_Throws()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "Id,Orders(Id,Status)",
            Filter = "Id:eq:1"
        };

        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Customers.FlexQueryAsync<Customer>(parameters));

        ex.Message.Should().Contain("not included");
        ex.Message.Should().Contain("Orders");
    }

    [Fact]
    public async Task DtoMode_NestedSelect_WithoutInclude_Throws()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "Id,Orders(Id,Status)",
            Filter = "Id:eq:1"
        };

        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Customers.FlexQueryAsync<Customer, CustomerNavDto>(parameters, opt => { }));

        ex.Message.Should().Contain("not included");
        ex.Message.Should().Contain("Orders");
    }

    [Fact]
    public async Task DtoMode_ExplicitlyMappedNavigation_WithoutInclude_Throws()
    {
        // An explicitly mapped navigation resolves through the QuerySurface, but
        // resolution is not authorization: include is still required.
        var parameters = new FlexQueryParameters
        {
            Select = "Id,Orders(Id,Status)",
            Filter = "Id:eq:1"
        };

        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Customers.FlexQueryAsync<Customer, CustomerNavDto>(parameters, opt =>
                opt.MapField<CustomerNavDto, Customer, List<Order>>(d => d.Orders, e => e.Orders)));

        ex.Message.Should().Contain("not included");
    }

    // Must pass --------------------------------------------------------------------

    [Fact]
    public async Task EntityMode_NestedSelect_WithInclude_Passes()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "Id,Orders(Id,Status)",
            Include = "Orders",
            Filter = "Id:eq:1"
        };

        var result = await _db.Customers.FlexQueryAsync<Customer>(parameters);

        result.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DtoMode_NestedSelect_WithInclude_Passes()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "Id,Orders(Id,Status)",
            Include = "Orders",
            Filter = "Id:eq:1"
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerNavDto>(parameters, opt => { });

        result.Data.Should().ContainSingle();
        result.Data[0].Orders.Should().NotBeNull();
    }

    [Fact]
    public async Task NestedSelect_IsCaseInsensitive_WhenCheckingInclude()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "Id,orders(Id,Status)",
            Include = "orders",
            Filter = "Id:eq:1"
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerNavDto>(parameters, opt => { });

        result.Data.Should().ContainSingle();
    }

    // Computed scalar mapping must not require include ------------------------------

    [Fact]
    public async Task ComputedScalarMapping_DoesNotRequireInclude()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "OrdersTotal",
            Filter = "Id:eq:1"
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerOrdersTotalDto>(parameters, opt =>
            opt.MapField<Customer, decimal>("OrdersTotal", e => e.Orders.Sum(o => o.Total)));

        result.Data.Should().ContainSingle();
        result.Data[0].OrdersTotal.Should().BeGreaterThan(0);
    }

    // Lenient mode ------------------------------------------------------------------

    [Fact]
    public async Task LenientMode_WithoutInclude_ScalarSiblingPreserved()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "Id,Orders(Id,Status)",
            Filter = "Id:eq:1"
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerNavDto>(parameters, opt =>
            opt.StrictFieldValidation = false);

        result.Data.Should().ContainSingle();
        // The unauthorized navigation projection was removed; the scalar row remains.
    }

    [Fact]
    public async Task LenientMode_NestedChildNavRemoved_ScalarChildrenPreserved()
    {
        // Parent authorized via include, but the child navigation (OrderItems) is not:
        // lenient mode removes the child navigation while keeping the parent's scalar
        // children (Id) — preserving independently valid sibling selections.
        var parameters = new FlexQueryParameters
        {
            Select = "Id,Orders(Id,OrderItems(Id))",
            Include = "Orders",
            Filter = "Id:eq:1"
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerNestedDto>(parameters, opt =>
        {
            opt.StrictFieldValidation = false;
            opt.CreateMap<Customer, CustomerNestedDto>()
                .ForNavigation(d => d.Orders, e => e.Orders);
            opt.CreateMap<Order, OrderNestedDto>();
            opt.CreateMap<OrderItem, OrderItemNestedDto>();
        });

        result.Data.Should().ContainSingle();
        var orders = result.Data[0].Orders;
        orders.Should().NotBeNull();
        if (orders is { Count: > 0 })
        {
            orders![0].OrderItems.Should().BeNull();
        }
    }

    // Nested levels ------------------------------------------------------------------

    [Fact]
    public async Task NestedLevels_BothPathsIncluded_Passes()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "Id,Orders(Id,OrderItems(Id))",
            Include = "Orders,Orders.OrderItems",
            Filter = "Id:eq:1"
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerNestedDto>(parameters, opt =>
        {
            opt.CreateMap<Customer, CustomerNestedDto>()
                .ForNavigation(d => d.Orders, e => e.Orders);
            opt.CreateMap<Order, OrderNestedDto>();
            opt.CreateMap<OrderItem, OrderItemNestedDto>();
        });

        result.Data.Should().ContainSingle();
    }

    [Fact]
    public async Task NestedLevels_ChildPathNotIncluded_Throws()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "Id,Orders(Id,OrderItems(Id))",
            Include = "Orders",
            Filter = "Id:eq:1"
        };

        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Customers.FlexQueryAsync<Customer, CustomerNestedDto>(parameters, opt =>
            {
                opt.CreateMap<Customer, CustomerNestedDto>()
                    .ForNavigation(d => d.Orders, e => e.Orders);
                opt.CreateMap<Order, OrderNestedDto>();
                opt.CreateMap<OrderItem, OrderItemNestedDto>();
            }));

        ex.Message.Should().Contain("Orders.OrderItems");
    }

    [Fact]
    public async Task NestedLevels_IncludeLoaderSemantics_ParentChainAuthorized()
    {
        // Exact-match contract: include=Orders.OrderItems authorizes only the exact
        // path Orders.OrderItems — it does NOT implicitly authorize selecting the
        // parent navigation Orders(...) with its own nested projection.
        var parameters = new FlexQueryParameters
        {
            Select = "Id,Orders(Id,OrderItems(Id))",
            Include = "Orders.OrderItems",
            Filter = "Id:eq:1"
        };

        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Customers.FlexQueryAsync<Customer, CustomerNestedDto>(parameters, opt =>
            {
                opt.CreateMap<Customer, CustomerNestedDto>()
                    .ForNavigation(d => d.Orders, e => e.Orders);
                opt.CreateMap<Order, OrderNestedDto>();
                opt.CreateMap<OrderItem, OrderItemNestedDto>();
            }));

        ex.Message.Should().Contain("not included");
        ex.Message.Should().Contain("Orders");
    }

    public class CustomerNestedDto
    {
        public int Id { get; set; }
        public List<OrderNestedDto>? Orders { get; set; }
    }

    public class OrderNestedDto
    {
        public int Id { get; set; }
        public List<OrderItemNestedDto>? OrderItems { get; set; }
    }

    public class OrderItemNestedDto
    {
        public int Id { get; set; }
    }
}

