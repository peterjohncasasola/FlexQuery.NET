using System.Reflection;
using FlexQuery.NET;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.EntityFrameworkCore.Options;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Fql;
using FlexQuery.NET.Tests.Shared.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace FlexQuery.NET.Tests.Integration;

/// <summary>
/// End-to-end regression for the nested include access contract on the entity endpoint:
/// <c>include=Orders,Orders.OrderItems</c> + <c>expand=Orders(...),Orders.OrderItems(...)</c>
/// with <c>AllowedIncludes</c> governance whitelisting both exact paths.
///
/// The reproduction request is FQL syntax (matching the reporter's trace through
/// <c>QueryOptionsExtensions.ValidateOrThrow → QueryValidator.Validate →
/// IncludeAccessValidationRule.Validate</c>). The request must pass validation and reach
/// the EF Core execution pipeline, and the EF Core nested expansion behavior
/// (per-customer single filtered order sorted descending, item take, correlation)
/// must be preserved.
/// </summary>
public class IncludeAccessGovernanceTests : IDisposable
{
    private readonly SharedTestDbContext _db = SharedTestDbContext.CreateInMemorySeeded();

    // Registers the FQL parser (additive, idempotent — no global default-syntax mutation).
    static IncludeAccessGovernanceTests() => Fql.Register();

    public void Dispose() => _db.Dispose();

    private const string Include = "Orders,Orders.OrderItems";
    private const string Expand = "Orders(take=1;filter=Status=\"Delivered\";sort=Id DESC),Orders.OrderItems(take=5)";

    private static QueryOptions ExactRequest() => new FlexQueryParameters
    {
        Include = Include,
        Expand = Expand
    }.ToQueryOptions(QuerySyntax.Fql);

    private static EfCoreQueryOptions Governance() => new()
    {
        AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Orders", "Orders.OrderItems" }
    };

    private static object? Prop(object row, string name) =>
        row.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(row);

    // Exact reproduction: explicitly declared parent + child include with both paths
    // whitelisted must pass IncludeAccessValidationRule and reach EF Core execution.

    [Fact]
    public async Task ExactRequest_BothPathsWhitelisted_PassesValidation_ReachesEfCoreExecution()
    {
        var result = await _db.Customers.FlexQueryAsync(ExactRequest(), Governance());

        result.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExactRequest_DeliveredFilter_TakeAndCorrelationPreserved()
    {
        var result = await _db.Customers.FlexQueryAsync(ExactRequest(), Governance());

        foreach (var row in result.Data)
        {
            var orders = Prop(row, "Orders") as IEnumerable<object> ?? [];
            var orderList = orders.ToList();

            // At most 1 delivered order per customer (Orders take=1 + Status="Delivered").
            orderList.Should().HaveCountLessThanOrEqualTo(1);

            foreach (var order in orderList)
            {
                Prop(order, "Status").Should().Be("Delivered");

                // At most 5 order items per selected order, correlated to it (no unrelated items).
                var items = Prop(order, "OrderItems") as IEnumerable<object> ?? [];
                var itemList = items.ToList();
                itemList.Should().HaveCountLessThanOrEqualTo(5);
                foreach (var item in itemList)
                {
                    Prop(item, "OrderId").Should().Be(Prop(order, "Id"));
                }
            }
        }

        // Bob (Id 2) is the only customer with a Delivered order in the shared seed data.
        var bob = result.Data.Single(c => (int)Prop(c, "Id")! == 2);
        var bobOrders = (Prop(bob, "Orders") as IEnumerable<object> ?? []).ToList();
        bobOrders.Should().ContainSingle().Which.Should().Match<object>(o => (int)Prop(o, "Id")! == 10003);
    }

    [Fact]
    public async Task ExactRequest_ShippedFilter_ItemsCorrelatedAndCapped()
    {
        // Alice (Id 1) has Shipped order 10001 with items 1,2 and Pending order 10002 with item 3:
        // the shipped filter selects only 10001, so item 3 (unrelated) must not load.
        var queryOptions = new FlexQueryParameters
        {
            Filter = "Id = 1",
            Include = Include,
            Expand = "Orders(take=1;filter=Status=\"Shipped\";sort=Id DESC),Orders.OrderItems(take=5)"
        }.ToQueryOptions(QuerySyntax.Fql);

        var result = await _db.Customers.FlexQueryAsync(queryOptions, Governance());

        var alice = result.Data.Should().ContainSingle().Subject;
        var orders = (Prop(alice, "Orders") as IEnumerable<object> ?? []).ToList();
        orders.Should().ContainSingle().Which.Should().Match<object>(o => (int)Prop(o, "Id")! == 10001);

        var items = (Prop(orders[0], "OrderItems") as IEnumerable<object> ?? []).ToList();
        items.Should().HaveCount(2);
        items.Select(i => (int)Prop(i, "Id")!).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task ExactRequest_ThreeLevelDeeper_IncludeWhitelisted_EndToEnd()
    {
        // Case 5 end-to-end: arbitrary depth is not hard-coded to two levels.
        var queryOptions = new FlexQueryParameters
        {
            Filter = "Id = 1",
            Include = "Orders,Orders.OrderItems,Orders.OrderItems.Discounts",
            Expand = "Orders(take=1;sort=Id DESC),Orders.OrderItems(take=5),Orders.OrderItems.Discounts(take=5)"
        }.ToQueryOptions(QuerySyntax.Fql);
        var execOptions = new EfCoreQueryOptions
        {
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Orders", "Orders.OrderItems", "Orders.OrderItems.Discounts" }
        };

        var act = async () => await _db.Customers.FlexQueryAsync(queryOptions, execOptions);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExactRequest_ParentOnlyWhitelisted_Strict_ThrowsExactReproductionMessage()
    {
        // Governance contract preserved: whitelisting only 'Orders' must NOT silently
        // authorize 'Orders.OrderItems'. This documents the exact error from the
        // reproduction report (strict mode).
        var execOptions = new EfCoreQueryOptions
        {
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Orders" }
        };

        var act = async () => await _db.Customers.FlexQueryAsync(ExactRequest(), execOptions);

        (await act.Should().ThrowAsync<QueryValidationException>())
            .Which.Message.Should().Be("Include path 'Orders.OrderItems' is not allowed.");
    }

    [Fact]
    public async Task ExactRequest_ParentOnlyWhitelisted_Lenient_PrunesChildKeepsParent()
    {
        var execOptions = new EfCoreQueryOptions
        {
            StrictFieldValidation = false,
            AllowedIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Orders" }
        };

        var result = await _db.Customers.FlexQueryAsync(ExactRequest(), execOptions);

        result.Data.Should().NotBeEmpty();
    }
}
