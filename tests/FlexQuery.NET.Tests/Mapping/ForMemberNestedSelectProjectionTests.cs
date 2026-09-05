using System.Data;
using FlexQuery.NET;
using FlexQuery.NET.Configuration;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.EntityFrameworkCore.Options;
using FlexQuery.NET.Mapping;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared.Fixtures;
using FlexQuery.NET.Tests.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FlexQuery.NET.Tests.Mapping;

/// <summary>
/// Regression tests for explicit <c>ForMember</c> mappings resolved through nested
/// <c>select</c> projections (collection element fields), plus pagination metadata
/// contract tests for the typed DTO pipeline.
///
/// The failing request shape was:
/// <code>
/// include=Orders&amp;expand=Orders(take=1)&amp;pageSize=1
/// &amp;select=CustomerId,CustomerFullName,Orders(OrderId,CustomerId,OrderDate,Status,DeliveryDate)
/// </code>
/// with <c>CreateMap&lt;Order, OrderResponse&gt;().ForMember(dto =&gt; dto.DeliveryDate,
/// entity =&gt; entity.ExpectedDeliveryDate)</c> — the nested <c>DeliveryDate</c> select
/// field silently projected <c>null</c> because the Dapper name rewriter resolved
/// nested children against the root surface instead of the registered nested TypeMap
/// (no <c>Order.DeliveryDate</c> exists).
/// </summary>
[Collection("GlobalMapping")]
public class ForMemberNestedSelectProjectionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SharedTestDbContext _context;

    public ForMemberNestedSelectProjectionTests()
    {
        global::FlexQuery.NET.Parsers.Fql.Fql.Register();

        FlexQueryMapping.Reset();

        // Mirrors the documented AddFlexQuery startup pattern (FlexQueryOptions.CreateMap
        // registers into the application-level FlexQueryMapping registry).
        var options = new FlexQueryOptions();
        options.CreateMap<Customer, CustomerResponse>()
            .ForMember(dto => dto.CustomerId, entity => entity.Id)
            .ForMember(dto => dto.CustomerFullName, entity => entity.Name);
        options.CreateMap<Order, OrderResponse>()
            .ForMember(dto => dto.OrderId, entity => entity.Id)
            .ForMember(dto => dto.DeliveryDate, entity => entity.ExpectedDeliveryDate);
        options.CreateMap<OrderItem, OrderItemResponse>();

        _context = SharedTestDbContext.CreateSqlite();
        SampleData.Seed(_context);

        _connection = (SqliteConnection)_context.Database.GetDbConnection();
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }

    public void Dispose()
    {
        FlexQueryMapping.Reset();
        _connection.Dispose();
        _context.Dispose();
    }

    // Shared DTO graph mirroring the documented host configuration ---------------

    public class CustomerResponse
    {
        public int CustomerId { get; set; }
        public string CustomerFullName { get; set; } = string.Empty;
        public List<OrderResponse> Orders { get; set; } = [];
    }

    public class OrderResponse
    {
        public int OrderId { get; set; }
        public int CustomerId { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<OrderItemResponse> OrderItems { get; set; } = [];
    }

    public class OrderItemResponse
    {
        public int OrderItemId { get; set; }
        public int Quantity { get; set; }
    }

    private static FlexQueryParameters P(string? select = null, string? include = null, string? expand = null,
        int page = 1, int pageSize = 25)
        => new()
        {
            Select = select,
            Include = include,
            Expand = expand,
            Page = page,
            PageSize = pageSize
        };

    private static Action<DapperQueryOptions> DapperFql => opt =>
        opt.QuerySyntax = global::FlexQuery.NET.Parsers.QuerySyntax.Fql;

    private static Action<EfCoreQueryOptions> EfFql => opt =>
        opt.QuerySyntax = global::FlexQuery.NET.Parsers.QuerySyntax.Fql;

    // --- Projection regression tests ---------------------------------------------

    // Test 1 — scalar DTO mapping through select (root-level ForMember).

    [Fact]
    public async Task Dapper_Select_ScalarRenamedField_MapsFromEntityProperty()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            P(select: "CustomerId,CustomerFullName", pageSize: 5), DapperFql);

        var alice = result.Data.Single(c => c.CustomerId == 1);
        alice.CustomerFullName.Should().Be("Alice Johnson");
    }

    [Fact]
    public async Task EfCore_Select_ScalarRenamedField_MapsFromEntityProperty()
    {
        var result = await _context.Customers.FlexQueryAsync<Customer, CustomerResponse>(
            P(select: "CustomerId,CustomerFullName", pageSize: 5), EfFql);

        var alice = result.Data.Single(c => c.CustomerId == 1);
        alice.CustomerFullName.Should().Be("Alice Johnson");
    }

    // Test 2 — nested DTO mapping through select (element-level ForMember):
    // OrderResponse.DeliveryDate must resolve to Order.ExpectedDeliveryDate.

    [Fact]
    public async Task Dapper_Select_NestedRenamedField_MapsFromEntityProperty()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            P(select: "CustomerId,Orders(OrderId,DeliveryDate)", include: "Orders", pageSize: 5), DapperFql);

        var alice = result.Data.Single(c => c.CustomerId == 1);
        var order = alice.Orders.Single(o => o.OrderId == 10001);
        order.DeliveryDate.Should().Be(new DateTime(2023, 1, 15),
            "OrderResponse.DeliveryDate must project Order.ExpectedDeliveryDate through the registered ForMember");
    }

    [Fact]
    public async Task EfCore_Select_NestedRenamedField_MapsFromEntityProperty()
    {
        var result = await _context.Customers.FlexQueryAsync<Customer, CustomerResponse>(
            P(select: "CustomerId,Orders(OrderId,DeliveryDate)", include: "Orders", pageSize: 5), EfFql);

        var alice = result.Data.Single(c => c.CustomerId == 1);
        var order = alice.Orders.Single(o => o.OrderId == 10001);
        order.DeliveryDate.Should().Be(new DateTime(2023, 1, 15));
    }

    // Test 3 — the exact reproduction request.

    [Fact]
    public async Task Dapper_ExactReproduction_ProjectionAndMetadata()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            P(
                select: "CustomerId,CustomerFullName,Orders(OrderId,CustomerId,OrderDate,Status,DeliveryDate)",
                include: "Orders",
                expand: "Orders(take=1)",
                pageSize: 1), DapperFql);

        result.Data.Should().ContainSingle("pageSize=1 returns exactly one root Customer");
        var alice = result.Data[0];
        alice.CustomerId.Should().Be(1);
        alice.CustomerFullName.Should().Be("Alice Johnson");

        var order = alice.Orders.Should().ContainSingle("expand=Orders(take=1) returns at most one order").Subject;
        order.OrderId.Should().Be(10001);
        order.DeliveryDate.Should().Be(new DateTime(2023, 1, 15),
            "DeliveryDate must not be null when ExpectedDeliveryDate has a value");
    }

    [Fact]
    public async Task EfCore_ExactReproduction_ProjectionAndMetadata()
    {
        var result = await _context.Customers.FlexQueryAsync<Customer, CustomerResponse>(
            P(
                select: "CustomerId,CustomerFullName,Orders(OrderId,CustomerId,OrderDate,Status,DeliveryDate)",
                include: "Orders",
                expand: "Orders(take=1)",
                pageSize: 1), EfFql);

        result.Data.Should().ContainSingle();
        var alice = result.Data[0];
        alice.CustomerFullName.Should().Be("Alice Johnson");
        alice.Orders.Should().ContainSingle().Which.DeliveryDate.Should().Be(new DateTime(2023, 1, 15));
    }

    // Nested renamed field must work without a root select of the navigation too
    // (navigation heads synthesized from includes).

    [Fact]
    public async Task Dapper_NestedRenamedField_WithoutRootSelect_StillMaps()
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            P(include: "Orders", pageSize: 5), DapperFql);

        var alice = result.Data.Single(c => c.CustomerId == 1);
        alice.Orders.Single(o => o.OrderId == 10001).DeliveryDate.Should().Be(new DateTime(2023, 1, 15));
    }

    // --- Pagination metadata contract tests ---------------------------------------
    //
    // Documented contract (QueryResult.cs): ResultCount = total rows produced by the
    // final query shape BEFORE paging (== TotalCount for non-grouped queries).
    // TotalPages derives from ResultCount ?? TotalCount. Expanded child rows never
    // inflate root metadata.

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 10)]
    [InlineData(2, 10)]
    [InlineData(1, 25)]
    public async Task Dapper_PaginationMetadata_FollowsBeforePagingContract(int page, int pageSize)
    {
        var result = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            P(include: "Orders", page: page, pageSize: pageSize), DapperFql);

        result.TotalCount.Should().Be(10, "the fixture seeds exactly 10 Customers");
        result.ResultCount.Should().Be(10, "non-grouped queries: ResultCount = matching rows before paging");
        result.Page.Should().Be(page);
        result.PageSize.Should().Be(pageSize);
        result.TotalPages.Should().Be((int)Math.Ceiling(10d / pageSize));
        result.HasNextPage.Should().Be(page < result.TotalPages);
        result.HasPreviousPage.Should().Be(page > 1);
        result.Data.Count.Should().BeLessThanOrEqualTo(pageSize);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 10)]
    [InlineData(2, 10)]
    [InlineData(1, 25)]
    public async Task EfCore_PaginationMetadata_FollowsBeforePagingContract(int page, int pageSize)
    {
        var result = await _context.Customers.FlexQueryAsync<Customer, CustomerResponse>(
            P(include: "Orders", page: page, pageSize: pageSize), EfFql);

        result.TotalCount.Should().Be(10);
        result.ResultCount.Should().Be(10);
        result.Page.Should().Be(page);
        result.PageSize.Should().Be(pageSize);
        result.TotalPages.Should().Be((int)Math.Ceiling(10d / pageSize));
        result.HasNextPage.Should().Be(page < result.TotalPages);
        result.HasPreviousPage.Should().Be(page > 1);
        result.Data.Count.Should().BeLessThanOrEqualTo(pageSize);
    }

    // Expanded child rows must never inflate root pagination metadata.

    [Fact]
    public async Task Dapper_ExpandTake1_DoesNotInflateRootMetadata()
    {
        var withoutExpand = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            P(include: "Orders", pageSize: 1), DapperFql);

        var withExpand = await _connection.FlexQueryAsync<Customer, CustomerResponse>(
            P(include: "Orders", expand: "Orders(take=1)", pageSize: 1), DapperFql);

        withExpand.TotalCount.Should().Be(withoutExpand.TotalCount);
        withExpand.ResultCount.Should().Be(withoutExpand.ResultCount);
        withExpand.TotalPages.Should().Be(withoutExpand.TotalPages);
        withExpand.HasNextPage.Should().Be(withoutExpand.HasNextPage);
        withExpand.HasPreviousPage.Should().Be(withoutExpand.HasPreviousPage);
        withExpand.Data.Should().ContainSingle();
        withExpand.Data[0].Orders.Should().HaveCountLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task EfCore_ExpandTake1_DoesNotInflateRootMetadata()
    {
        var withoutExpand = await _context.Customers.FlexQueryAsync<Customer, CustomerResponse>(
            P(include: "Orders", pageSize: 1), EfFql);

        var withExpand = await _context.Customers.FlexQueryAsync<Customer, CustomerResponse>(
            P(include: "Orders", expand: "Orders(take=1)", pageSize: 1), EfFql);

        withExpand.TotalCount.Should().Be(withoutExpand.TotalCount);
        withExpand.ResultCount.Should().Be(withoutExpand.ResultCount);
        withExpand.TotalPages.Should().Be(withoutExpand.TotalPages);
        withExpand.HasNextPage.Should().Be(withoutExpand.HasNextPage);
        withExpand.HasPreviousPage.Should().Be(withoutExpand.HasPreviousPage);
        withExpand.Data.Should().ContainSingle();
        withExpand.Data[0].Orders.Should().HaveCountLessThanOrEqualTo(1);
    }
}
