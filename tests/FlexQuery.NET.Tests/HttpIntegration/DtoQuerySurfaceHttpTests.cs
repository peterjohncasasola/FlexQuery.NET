using System.Net;
using System.Text.Json.Nodes;
using FlexQuery.NET.Samples.WebApi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FlexQuery.NET.Tests.HttpIntegration;

/// <summary>
/// End-to-end tests through the real ASP.NET Core HTTP pipeline
/// (WebApplicationFactory over the sample Web API host).
///
/// Proves the DTO-aware query surface contract works over actual HTTP:
/// the response DTO is the public query surface — a renamed field
/// (<c>CustomerName</c> → entity <c>FirstName</c> via MapField) is queryable
/// across select/filter/sort/group/aggregate, and entity-only fields are rejected.
/// </summary>
public class DtoQuerySurfaceHttpTests : IClassFixture<DtoQuerySurfaceHttpTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        // A single open in-memory connection keeps one shared SQLite database alive for
        // every DbContext instance created by the host (each ':memory:' connection is
        // otherwise an isolated, empty database).
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection =
            new("DataSource=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _connection.Open();

            builder.ConfigureServices(services =>
            {
                var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                services.Remove(descriptor);
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _connection.Dispose();
            base.Dispose(disposing);
        }
    }

    private readonly HttpClient _client;

    public DtoQuerySurfaceHttpTests(Factory factory)
        => _client = factory.CreateClient();

    private static JsonNode GetData(string body)
        => JsonNode.Parse(body)!["data"]!;

    // Select -------------------------------------------------------------------

    [Fact]
    public async Task Select_RenamedField_ReturnsPublicName()
    {
        var response = await _client.GetAsync("/api/ef/customers/summary?select=Id,CustomerName,Email&pageSize=2");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var item = GetData(await response.Content.ReadAsStringAsync())[0]!.AsObject();
        item.ContainsKey("customerName").Should().BeTrue();
        item.ContainsKey("name").Should().BeFalse();
        item.ContainsKey("firstName").Should().BeFalse();
        item["customerName"]!.GetValue<string>().Should().NotBeNullOrEmpty();
    }

    // Filter --------------------------------------------------------------------

    [Fact]
    public async Task Filter_RenamedField_ResolvesThroughMapField()
    {
        var response = await _client.GetAsync("/api/ef/customers/summary?filter=CustomerName:eq:Melissa");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        node["totalCount"]!.GetValue<int>().Should().BeGreaterThan(0);
        var names = GetData(node.ToString()!)!.AsArray()
            .Select(i => i!["customerName"]!.GetValue<string>())
            .ToList();
        names.Should().OnlyContain(n => n == "Melissa");
    }

    [Fact]
    public async Task Filter_EntityOnlyField_IsRejected()
    {
        // 'FirstName' exists on the entity but is not part of the public surface of
        // CustomerSummaryDto — the HTTP response must be a validation failure.
        var response = await _client.GetAsync("/api/ef/customers/summary?filter=FirstName:eq:Melissa");
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("public query surface");
    }

    [Fact]
    public async Task Filter_NonExposedEntityField_IsRejected()
    {
        // 'Salary' exists on the entity, is not on the DTO surface at all.
        var response = await _client.GetAsync("/api/ef/customers/summary?filter=Salary:gt:0");
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("public query surface");
    }

    // Sort ------------------------------------------------------------------------

    [Fact]
    public async Task Sort_RenamedField_Ascending()
    {
        var response = await _client.GetAsync("/api/ef/customers/summary?sort=CustomerName:asc&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var names = GetData(await response.Content.ReadAsStringAsync())!.AsArray()
            .Select(i => i!["customerName"]!.GetValue<string>())
            .ToList();
        names.Should().HaveCount(10);
        // First names are non-unique; assert non-decreasing order instead of uniqueness.
        var sorted = names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        names.SequenceEqual(sorted).Should().BeTrue();
    }

    [Fact]
    public async Task Sort_EntityOnlyField_IsRejected()
    {
        var response = await _client.GetAsync("/api/ef/customers/summary?sort=FirstName:asc");
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("public query surface");
    }

    // Group ---------------------------------------------------------------------

    [Fact]
    public async Task Group_RenamedField_PreservesPublicIdentity()
    {
        var response = await _client.GetAsync("/api/ef/customers/summary?groupby=CustomerName&pageSize=5");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var groups = GetData(node.ToString()!)!.AsArray();
        groups.Should().NotBeEmpty();

        var keys = groups.Select(g => g!.AsObject()).ToList();
        keys.All(g => g.ContainsKey("customerName")).Should().BeTrue();
        keys.All(g => !g.ContainsKey("firstName") && !g.ContainsKey("name")).Should().BeTrue();
    }

    // Aggregate --------------------------------------------------------------------

    [Fact]
    public async Task Aggregate_Count_OnRenamedField_UsesPublicAlias()
    {
        var response = await _client.GetAsync("/api/ef/customers/summary?aggregate=count:CustomerName:total");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        // Aggregate results belong to QueryResult.Aggregates metadata (public field
        // identity) — not to row data, and no aggregate alias property is required.
        var aggregates = node["aggregates"]!.AsObject();
        // Aggregate metadata keys preserve the raw field identity (consistent with the
        // non-DTO aggregate contract; dictionary keys are not camelCased).
        aggregates["CustomerName"]!["count"]!.GetValue<int>().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Aggregate_GroupBy_RenamedField_PublicIdentityPreserved()
    {
        // Grouped result rows are their own result shape (group key + aggregate aliases).
        // CustomerSummaryDto does not model the 'total' alias — the rows keep the dynamic
        // grouped shape with the public group identity and the aggregate alias.
        var response = await _client.GetAsync("/api/ef/customers/summary?groupby=CustomerName&aggregate=count:CustomerName:total&pageSize=3");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var rows = GetData(body)!.AsArray();
        rows.Should().NotBeEmpty();

        foreach (var row in rows)
        {
            var obj = row!.AsObject();
            obj.ContainsKey("customerName").Should().BeTrue();
            obj.ContainsKey("firstName").Should().BeFalse();
        }

        body.ToLowerInvariant().Should().Contain("total");
    }

    [Fact]
    public async Task Aggregate_EntityOnlyField_IsRejected()
    {
        var response = await _client.GetAsync("/api/ef/customers/summary?aggregate=count:FirstName:total");
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("FirstName");
    }

    // Include / Expand ---------------------------------------------------------

    [Fact]
    public async Task Include_OnDtoNavigation_LoadsRelatedCollection()
    {
        var response = await _client.GetAsync("/api/ef/customers/dto?filter=Id:eq:1&include=Orders&pageSize=1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var item = GetData(await response.Content.ReadAsStringAsync())[0]!.AsObject();
        item["orders"].Should().NotBeNull();
        item["orders"]!.AsArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Expand_WithSortAndTake_OnDtoNavigation_AppliesElementOptions()
    {
        // Regression: expand sort previously rebound the root surface expression onto the
        // Order element parameter and threw ArgumentException.
        var response = await _client.GetAsync("/api/ef/customers/dto?filter=Id:eq:1&include=Orders&expand=Orders(sort=Id:desc;take=2)&pageSize=1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var orders = GetData(await response.Content.ReadAsStringAsync())[0]!["orders"]!.AsArray();
        orders.Should().HaveCount(2);
        var ids = orders.Select(o => o!["id"]!.GetValue<int>()).ToList();
        ids.SequenceEqual(ids.OrderByDescending(i => i)).Should().BeTrue();
    }

    [Fact]
    public async Task Expand_WithFilter_OnDtoNavigation_FiltersElementCollection()
    {
        var response = await _client.GetAsync("/api/ef/customers/dto?filter=Id:eq:1&include=Orders&expand=Orders(filter=Id:eq:93)&pageSize=1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var orders = GetData(await response.Content.ReadAsStringAsync())[0]!["orders"]!.AsArray();
        orders.Should().ContainSingle();
        orders[0]!["id"]!.GetValue<int>().Should().Be(93);
    }

    // Include / Expand through DTO-mapped (renamed) navigations ----------------------

    [Fact]
    public async Task Include_RenamedNavigation_ResolvesThroughMapField()
    {
        // 'Purchases' maps to the entity's 'Orders' collection via MapField.
        var response = await _client.GetAsync("/api/ef/customers/purchases?filter=Id:eq:1&include=Purchases&pageSize=1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var item = GetData(await response.Content.ReadAsStringAsync())[0]!.AsObject();
        item["purchases"].Should().NotBeNull();
        item["purchases"]!.AsArray().Should().NotBeEmpty();
        item.ContainsKey("orders").Should().BeFalse();
    }

    [Fact]
    public async Task Expand_WithSortAndTake_OnRenamedNavigation_AppliesElementOptions()
    {
        var response = await _client.GetAsync("/api/ef/customers/purchases?filter=Id:eq:1&include=Purchases&expand=Purchases(take=2%3B%20sort=Id:desc)&pageSize=1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var purchases = GetData(await response.Content.ReadAsStringAsync())[0]!["purchases"]!.AsArray();
        purchases.Should().HaveCount(2);
        var ids = purchases.Select(o => o!["id"]!.GetValue<int>()).ToList();
        ids.SequenceEqual(ids.OrderByDescending(i => i)).Should().BeTrue();
    }

    [Fact]
    public async Task Include_EntityOnlyNavigation_IsRejected()
    {
        // 'Orders' exists on the entity but the DTO exposes it as 'Purchases' —
        // the entity name must not bypass the public query surface.
        var response = await _client.GetAsync("/api/ef/customers/purchases?filter=Id:eq:1&include=Orders&pageSize=1");
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Orders");
    }

    // Serialization safety ------------------------------------------------------------------

    [Fact]
    public async Task Include_WithBidirectionalNavigation_SerializesWithoutCycle()
    {
        // Customer.Orders <-> Order.Customer is a bidirectional graph. FlexQuery's default
        // no-tracking execution does not populate inverse navigations, and the host's
        // ReferenceHandler.IgnoreCycles guards any host-side fixup — the response must
        // serialize instead of throwing a depth-32 JsonException.
        var response = await _client.GetAsync("/api/ef/customers?filter=Id:eq:1&include=Orders&pageSize=1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var item = JsonNode.Parse(body)!["data"]![0]!.AsObject();
        item["orders"].Should().NotBeNull();
        item["orders"]!.AsArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Include_WithTrackedQueryAndInverseFixup_SerializesWithoutCycle()
    {
        // Reproduces the host-side scenario: UseNoTracking = false makes EF populate the
        // inverse navigation (Order.Customer) via relationship fixup, producing a
        // genuinely cyclic object graph inside QueryResult.Data. The shape converter's
        // cycle-safe serialization must emit the response instead of a depth-32
        // JsonException.
        var response = await _client.GetAsync("/api/ef/customers/tracked?filter=Id:eq:1&include=Orders&pageSize=1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var item = JsonNode.Parse(body)!["data"]![0]!.AsObject();
        item["orders"]!.AsArray().Should().NotBeEmpty();
        body.Should().NotContain("object cycle");
    }

    // Include/Expand windowing + response-shape contract --------------------------------

    [Fact]
    public async Task Include_Expand_FilterSortTake_AppliesWindowAtDatabaseAndCutsGraph()
    {
        // Mirrors the user's request shape: root pageSize=1 with a filtered/sorted/taken
        // expand. The expand window must apply (filter + take), root paging must limit the
        // customer set first, and the response must not contain nested navigation graphs.
        var response = await _client.GetAsync(
            "/api/ef/customers/dto?filter=Id:eq:1&include=Orders&expand=Orders(filter=Status:eq:Pending%3B%20sort=Id:desc%3B%20take=2)&pageSize=1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body)!;
        node["totalCount"]!.GetValue<int>().Should().Be(1);
        node["data"]!.AsArray().Should().ContainSingle();

        var orders = node["data"]![0]!["orders"]!.AsArray();
        orders.Count.Should().BeLessThanOrEqualTo(2);
        foreach (var order in orders)
        {
            order!["status"]!.GetValue<string>().Should().Be("Pending");
            // Reverse navigation / unrequested navigations must not enter the response.
            order.AsObject().ContainsKey("customer").Should().BeFalse();
            order.AsObject().ContainsKey("orderItems").Should().BeFalse();
        }
    }

    [Fact]
    public async Task Include_Expand_TakeOnly_LimitsCollectionSize()
    {
        var response = await _client.GetAsync(
            "/api/ef/customers/dto?filter=Id:eq:1&include=Orders&expand=Orders(take=3; sort=Id:desc)&pageSize=1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var orders = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["data"]![0]!["orders"]!.AsArray();
        orders.Count.Should().BeLessThanOrEqualTo(3);
        var ids = orders.Select(o => o!["id"]!.GetValue<int>()).ToList();
        ids.SequenceEqual(ids.OrderByDescending(i => i)).Should().BeTrue();
    }

    [Fact]
    public async Task NarrowRootSelect_WithExpand_RootAndNavigationScopesStayIndependent()
    {
        // Root select applies to the root DTO only; the expanded Orders keep their own
        // default projection (full entity scalar shape) because no nested select exists.
        // Filter-only fields (LastName) participate in the WHERE clause but must not
        // appear in the public output.
        var response = await _client.GetAsync(
            "/api/ef/customers/dto?select=firstName&filter=lastName:eq:Williams&include=Orders&expand=Orders(take=2; sort=Id:desc)&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body)!;
        var customers = node["data"]!.AsArray();
        customers.Should().NotBeEmpty();

        foreach (var customer in customers)
        {
            var obj = customer!.AsObject();
            // Root scope: only the selected public field plus the expand.
            obj.ContainsKey("firstName").Should().BeTrue();
            obj.ContainsKey("lastName").Should().BeFalse(); // filter-only field never leaks
            obj.ContainsKey("email").Should().BeFalse();

            // Orders keep their own default projection: entity scalars present.
            var orders = obj["orders"]!.AsArray();
            orders.Count.Should().BeLessThanOrEqualTo(2);
            foreach (var order in orders)
            {
                order!.AsObject().ContainsKey("orderNumber").Should().BeTrue();
                order.AsObject().ContainsKey("totalAmount").Should().BeTrue();
                order.AsObject().ContainsKey("customer").Should().BeFalse();
            }
        }
    }

    [Fact]
    public async Task NestedSelectTree_WithExpand_NestedShapeFromRootSelectOnly()
    {
        // The exact failing scenario: nested select tree in the ROOT select (no select
        // inside expand) + expand window. Nested objects must expose only the selected
        // fields; expand constraints (filter/sort/take) still apply.
        var response = await _client.GetAsync(
            "/api/ef/customers/order-summaries?include=orders&expand=orders(take=3; filter=status:eq:Pending; sort=id:desc)&pageSize=10&select=customerName,orders(id,orderDate,status)");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body)!;
        var customers = node["data"]!.AsArray();
        customers.Should().NotBeEmpty();

        foreach (var customer in customers)
        {
            var obj = customer!.AsObject();
            obj.ContainsKey("customerName").Should().BeTrue();

            var orders = obj["orders"]!.AsArray();
            orders.Count.Should().BeLessThanOrEqualTo(3);

            foreach (var order in orders)
            {
                var orderObj = order!.AsObject();
                // Only the selected nested fields may appear.
                orderObj.ContainsKey("id").Should().BeTrue();
                orderObj.ContainsKey("orderDate").Should().BeTrue();
                orderObj.ContainsKey("status").Should().BeTrue();
                // Unselected entity columns must not leak.
                orderObj.ContainsKey("orderNumber").Should().BeFalse();
                orderObj.ContainsKey("totalAmount").Should().BeFalse();
                orderObj.ContainsKey("customerId").Should().BeFalse();
                orderObj.ContainsKey("customer").Should().BeFalse();

                orderObj["status"]!.GetValue<string>().Should().Be("Pending");
            }

            var ids = orders.Select(o => o!["id"]!.GetValue<int>()).ToList();
            ids.SequenceEqual(ids.OrderByDescending(i => i)).Should().BeTrue();
        }
    }

    [Fact]
    public async Task NestedSelectTree_WithoutExpand_NestedShapeStillApplies()
    {
        var response = await _client.GetAsync(
            "/api/ef/customers/order-summaries?filter=id:eq:1&include=orders&select=customerName,orders(id,status)");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var orders = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["data"]![0]!["orders"]!.AsArray();
        orders.Should().NotBeEmpty();
        foreach (var order in orders)
        {
            var orderObj = order!.AsObject();
            orderObj.ContainsKey("id").Should().BeTrue();
            orderObj.ContainsKey("status").Should().BeTrue();
            orderObj.ContainsKey("orderDate").Should().BeFalse();
            orderObj.ContainsKey("orderNumber").Should().BeFalse();
        }
    }

    [Fact]
    public async Task SortQueryKey_SpaceDirection_MappedRootField_SortsViaHttpBinding()
    {
        // The space-direction sort form binds through [FromQuery] (FlexQueryBase.Sort)
        // and resolves through the DTO surface (customerName → entity FirstName).
        var response = await _client.GetAsync(
            "/api/ef/customers/order-summaries?sort=customerName%20ASC&pageSize=10&select=customerName");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var names = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["data"]!.AsArray()
            .Select(c => c!["customerName"]!.GetValue<string>())
            .ToList();
        names.Should().HaveCount(10);
        var sorted = names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        names.SequenceEqual(sorted).Should().BeTrue("HTTP sort must produce root ordering: [{0}]", string.Join(",", names));
    }

    [Fact]
    public async Task SortQueryKey_SpaceDirection_EntityOnlyName_IsRejected()
    {
        var response = await _client.GetAsync(
            "/api/ef/customers/order-summaries?sort=firstName%20ASC&select=customerName&pageSize=5");
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.ToLowerInvariant().Should().Contain("firstname");
    }

    // Same-name convention + default projection -------------------------------------

    [Fact]
    public async Task SameNameFields_WorkWithoutMapField()
    {
        var response = await _client.GetAsync("/api/ef/customers/dto?filter=Email:contains:williams1@example.com&select=Id,FirstName,Email");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var item = GetData(await response.Content.ReadAsStringAsync())[0]!.AsObject();
        item.ContainsKey("email").Should().BeTrue();
        item["email"]!.GetValue<string>().Should().Be("melissa.williams1@example.com");
    }

    [Fact]
    public async Task DefaultProjection_ExposesOnlyDtoScalars()
    {
        var response = await _client.GetAsync("/api/ef/customers/summary?pageSize=1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var item = GetData(await response.Content.ReadAsStringAsync())[0]!.AsObject();
        item.ContainsKey("id").Should().BeTrue();
        item.ContainsKey("customerName").Should().BeTrue();
        item.ContainsKey("email").Should().BeTrue();
        // Entity-only fields must never appear.
        item.ContainsKey("firstName").Should().BeFalse();
        item.ContainsKey("city").Should().BeFalse();
        item.ContainsKey("status").Should().BeFalse();
        item.ContainsKey("salary").Should().BeFalse();
        item.ContainsKey("createdDate").Should().BeFalse();
    }
}


