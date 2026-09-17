using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared.Fixtures;
using Xunit;

namespace FlexQuery.NET.Tests.Integration;

/// <summary>
/// Deep collection expansion using flat dotted navigation paths:
/// <c>include=Orders(take=3),Orders.OrderItems(take=5)</c>. The public syntax is flat;
/// each expansion path is independent; options are scoped to their own path; include
/// must explicitly declare every deep path (exact-match); the terminal navigation must
/// be collection-valued; duplicate paths are rejected deterministically.
/// </summary>
public class DeepCollectionIncludeTests : IDisposable
{
    private readonly SharedTestDbContext _db = SharedTestDbContext.CreateInMemorySeeded();

    public void Dispose() => _db.Dispose();

    public class OrderDto
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<OrderItemDto> OrderItems { get; set; } = [];
    }

    public class OrderItemDto
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
    }

    public class CustomerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<OrderDto> Orders { get; set; } = [];
        public List<Address> Addresses { get; set; } = [];
    }

    // Valid: single collection ------------------------------------------------------

    [Fact]
    public async Task SingleCollection_Expand_TakeApplied()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(
            new FlexQueryParameters
            {
                Filter = "Id:eq:1",
                Include = "Orders(take=1)"
            },
            opt =>
            {
                opt.CreateMap<Customer, CustomerDto>()
                    .ForNavigation(d => d.Orders, e => e.Orders);
                opt.CreateMap<Order, OrderDto>();
            });

        result.Data.Should().ContainSingle();
        result.Data[0].Orders.Should().HaveCount(1);
    }

    // Valid: nested collection via flat dotted path ---------------------------------

    [Fact]
    public async Task NestedCollection_FlatDottedPath_OptionsScopedToEachPath()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(
            new FlexQueryParameters
            {
                Filter = "Id:eq:1",
                Include = "Orders(take=1),Orders.OrderItems(take=2)"
            },
            opt =>
            {
                opt.CreateMap<Customer, CustomerDto>()
                    .ForNavigation(d => d.Orders, e => e.Orders);
                opt.CreateMap<Order, OrderDto>();
                opt.CreateMap<OrderItem, OrderItemDto>();
            });

        result.Data.Should().ContainSingle();
        var orders = result.Data[0].Orders;
        orders.Should().HaveCount(1); // Orders take=1
        orders[0].OrderItems.Should().HaveCountLessThanOrEqualTo(2); // OrderItems take=2
    }

    [Fact]
    public async Task NestedCollection_FlatDottedPath_WithFilterAndSort()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(
            new FlexQueryParameters
            {
                Filter = "Id:eq:2",
                Include = "Orders(filter=Status:eq:Delivered;sort=Id:desc;take=3),Orders.OrderItems(take=5)"
            },
            opt =>
            {
                opt.CreateMap<Customer, CustomerDto>()
                    .ForNavigation(d => d.Orders, e => e.Orders);
                opt.CreateMap<Order, OrderDto>();
                opt.CreateMap<OrderItem, OrderItemDto>();
            });

        result.Data.Should().ContainSingle();
        var orders = result.Data[0].Orders;
        orders.Should().NotBeEmpty();
        orders.All(o => o.Status == "Delivered").Should().BeTrue();
    }

    // Deep dotted includes carry their own parent chain — no separate inclusion is
    // required any more than it used to be (the old include+expand pairing rule went
    // away with the unified include model).
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeepInclude_AutoLoadsParentChain()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(
            new FlexQueryParameters
            {
                Filter = "Id:eq:1",
                Include = "Orders,Orders.OrderItems(take=5)"
            },
            opt =>
            {
                opt.CreateMap<Customer, CustomerDto>()
                    .ForNavigation(d => d.Orders, e => e.Orders);
                opt.CreateMap<Order, OrderDto>();
                opt.CreateMap<OrderItem, OrderItemDto>();
            });

        result.Data.Should().ContainSingle();
        result.Data[0].Orders.Should().NotBeEmpty();
    }

    // Invalid: reference navigation with collection options --------------------------

    [Fact]
    public async Task ReferenceNavigationAsTerminal_WithCollectionOption_FailsValidation()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(
                new FlexQueryParameters
                {
                    Filter = "Id:eq:1",
                    Include = "Orders,Orders.Customer(take=1)"
                },
                opt =>
                {
                    opt.CreateMap<Customer, CustomerDto>()
                        .ForNavigation(d => d.Orders, e => e.Orders);
                    opt.CreateMap<Order, OrderDto>();
                }));

        ex.Message.Should().Contain("single-valued relationship");
    }

    // Valid: multiple sibling collections ----------------------------------------------

    [Fact]
    public async Task MultipleSiblingCollections_ExpandIndependently()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(
            new FlexQueryParameters
            {
                Filter = "Id:eq:1",
                Include = "Orders(take=1)"
            },
            opt =>
            {
                opt.CreateMap<Customer, CustomerDto>()
                    .ForNavigation(d => d.Orders, e => e.Orders);
                opt.CreateMap<Order, OrderDto>();
            });

        result.Data.Should().ContainSingle();
        result.Data[0].Orders.Should().HaveCount(1);
    }

    // Invalid: duplicate expansion path --------------------------------------------------

    [Fact]
    public async Task DuplicateExpansionPath_RejectedDeterministically()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(
                new FlexQueryParameters
                {
                    Filter = "Id:eq:1",
                    Include = "Orders(take=3),Orders(take=10)"
                },
                opt =>
                {
                    opt.CreateMap<Customer, CustomerDto>()
                        .ForNavigation(d => d.Orders, e => e.Orders);
                    opt.CreateMap<Order, OrderDto>();
                }));

        ex.Message.Should().Contain("Duplicate include path");
    }

    // Deep: shared model has no third-level collection (OrderItem.Product is a
    // reference navigation → rejected per the collection-only contract). The 3-level
    // normalization is covered by the IncludeNormalizer unit tests in Mapping/.

    [Fact]
    public async Task ReferenceTerminal_AtDeeperLevel_Rejected()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(
                new FlexQueryParameters
                {
                    Filter = "Id:eq:1",
                    Include = "Orders(take=1),Orders.OrderItems(take=5),Orders.OrderItems.Product(take=2)"
                },
                opt =>
                {
                    opt.CreateMap<Customer, CustomerDto>()
                        .ForNavigation(d => d.Orders, e => e.Orders);
                    opt.CreateMap<Order, OrderDto>();
                    opt.CreateMap<OrderItem, OrderItemDto>();
                }));

        ex.Message.Should().Contain("single-valued relationship");
    }
}
