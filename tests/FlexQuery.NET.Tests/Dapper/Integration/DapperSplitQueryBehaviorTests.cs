using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Fql;

namespace FlexQuery.NET.Tests.Dapper.Integration;

public class DapperSplitQueryBehaviorTests : DapperApiTestBase
{
    [Fact]
    public async Task Include_Should_Not_Truncate_Child_Collection_By_Root_PageSize()
    {
        var response = await Client.GetAsync("/api/users?include=orders&pageSize=1&select=id,orders(id)");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = json.GetProperty("Data").EnumerateArray().ToList();

        items.Should().HaveCount(1);
        var orders = items[0].GetProperty("Orders").EnumerateArray().ToList();
        orders.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Alias_Projection_Should_Resolve_Original_Property()
    {
        var response = await Client.GetAsync("/api/users?select=id,name");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = json.GetProperty("Data").EnumerateArray().ToList();

        items.Should().NotBeEmpty();
        var first = items[0];
        Console.WriteLine($"DEBUG: first JSON = {first.GetRawText()}");
        Console.WriteLine($"DEBUG: first properties = {string.Join(", ", first.EnumerateObject().Select(p => p.Name))}");
        first.TryGetProperty("Id", out var id).Should().BeTrue();
        first.TryGetProperty("Name", out var name).Should().BeTrue();
    }

    [Fact]
    public async Task Explicit_Select_Should_Return_Only_Requested_Fields()
    {
        var response = await Client.GetAsync("/api/users?select=id,name");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = json.GetProperty("Data").EnumerateArray().ToList();

        items.Should().NotBeEmpty();
        var first = items[0];
        first.TryGetProperty("Id", out _).Should().BeTrue();
        first.TryGetProperty("Name", out _).Should().BeTrue();
        first.EnumerateObject().Count().Should().BeLessOrEqualTo(2);
    }

    [Fact]
    public async Task Expand_Should_Not_Remove_Root_Entities()
    {
        var response = await Client.GetAsync("/api/users?include=Orders&pageSize=1");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = json.GetProperty("Data").EnumerateArray().ToList();

        items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Include_Should_Omit_Unrequested_Nested_Navigations_From_Response()
    {
        var response = await Client.GetAsync("/api/users?include=orders&filter=id:eq:1&sort=id:asc&pageSize=1");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var first = json.GetProperty("Data").EnumerateArray().Single();

        first.TryGetProperty("Orders", out var ordersElement).Should().BeTrue();
        first.TryGetProperty("Profile", out _).Should().BeFalse();
        first.TryGetProperty("Address", out _).Should().BeFalse();
        first.TryGetProperty("Addresses", out _).Should().BeFalse();

        var firstOrder = ordersElement.EnumerateArray().First();
        firstOrder.TryGetProperty("Id", out _).Should().BeTrue();
        firstOrder.TryGetProperty("CustomerId", out _).Should().BeTrue();
        firstOrder.TryGetProperty("Customer", out _).Should().BeFalse();
        firstOrder.TryGetProperty("OrderItems", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Root_Pagination_Should_Affect_Only_Root_Count()
    {
        var response = await Client.GetAsync("/api/users?include=orders&pageSize=2");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = json.GetProperty("Data").EnumerateArray().ToList();

        items.Count.Should().BeLessOrEqualTo(2);
        items.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Expand_Should_Filter_Sort_And_Take_In_Loaded_Navigation()
    {
        await Connection.ExecuteAsync("""
            INSERT INTO Orders (Id, CustomerId, OrderDate, Status, Total, Price, Category, Number)
            VALUES
                (9101, 1, '2024-01-01', 'Delivered', 10, 10, 'Books', 'D-001'),
                (9102, 1, '2024-01-02', 'Delivered', 20, 20, 'Books', 'D-002'),
                (9103, 1, '2024-01-03', 'Delivered', 30, 30, 'Books', 'D-003'),
                (9104, 1, '2024-01-04', 'Delivered', 40, 40, 'Books', 'D-004'),
                (9105, 1, '2024-01-05', 'Delivered', 50, 50, 'Books', 'D-005'),
                (9106, 1, '2024-01-06', 'Pending', 60, 60, 'Books', 'P-006');
            """);

        var expand = Uri.EscapeDataString("orders(filter=status:eq:Delivered; sort=id:desc; take=3)");
        var response = await Client.GetAsync($"/api/users?include=orders&expand={expand}&select=id,name,orders(id,status)&sort=id:asc&pageSize=1");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var first = json.GetProperty("Data").EnumerateArray().First();
        var orders = first.GetProperty("Orders").EnumerateArray().ToList();

        orders.Should().HaveCount(3);
        orders.Select(order => order.GetProperty("Id").GetInt32()).Should().Equal(9105, 9104, 9103);
        orders.Should().OnlyContain(order => order.GetProperty("Status").GetString() == "Delivered");
    }

    [Fact]
    public async Task DictionaryOverload_Should_Preserve_Expand_Take_For_Dapper_Loading()
    {
        await Connection.ExecuteAsync("""
            INSERT INTO Orders (Id, CustomerId, OrderDate, Status, Total, Price, Category, Number)
            VALUES
                (9201, 1, '2024-02-01', 'Delivered', 10, 10, 'Books', 'D-001'),
                (9202, 1, '2024-02-02', 'Delivered', 20, 20, 'Books', 'D-002'),
                (9203, 1, '2024-02-03', 'Delivered', 30, 30, 'Books', 'D-003'),
                (9204, 1, '2024-02-04', 'Delivered', 40, 40, 'Books', 'D-004'),
                (9205, 1, '2024-02-05', 'Delivered', 50, 50, 'Books', 'D-005'),
                (9206, 1, '2024-02-06', 'Pending', 60, 60, 'Books', 'P-006');
            """);

        var parameters = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
        {
            ["include"] = "orders",
            ["expand"] = "orders(filter=status:eq:Delivered; sort=id:desc; take=3)",
            ["filter"] = "id:eq:1",
            ["sort"] = "id:asc"
        };

        var result = await ((DbConnection)Connection).FlexQueryAsync<Customer>(parameters, opt =>
        {
            opt.UseModel(SharedFlexQueryModel.Instance);
        });

        dynamic customer = result.Data.Single();
        var orders = ((IEnumerable<object>)customer.Orders).Cast<dynamic>().ToList();

        orders.Should().HaveCount(3);
        var orderIds = orders.Select(order => (int)order.Id).ToList();
        orderIds.Should().Equal(9205, 9204, 9203);
        
        foreach(var order in orders)
        {
            ((string)order.Status).Should().Be("Delivered");
            ((object?)order.GetType().GetProperty("Customer")).Should().BeNull();
            ((object?)order.GetType().GetProperty("OrderItems")).Should().BeNull();
        }
    }

    [Fact]
    public async Task FqlExpand_Should_Filter_Sort_And_Take_In_Dapper_Loaded_Navigation()
    {
        Fql.Register();

        await Connection.ExecuteAsync("""
            INSERT INTO Orders (Id, CustomerId, OrderDate, Status, Total, Price, Category, Number)
            VALUES
                (9301, 1, '2024-03-01', 'Delivered', 10, 10, 'Books', 'D-001'),
                (9302, 1, '2024-03-02', 'Delivered', 20, 20, 'Books', 'D-002'),
                (9303, 1, '2024-03-03', 'Delivered', 30, 30, 'Books', 'D-003'),
                (9304, 1, '2024-03-04', 'Delivered', 40, 40, 'Books', 'D-004'),
                (9305, 1, '2024-03-05', 'Delivered', 50, 50, 'Books', 'D-005'),
                (9306, 1, '2024-03-06', 'Pending', 60, 60, 'Books', 'P-006');
            """);

        var parameters = new FlexQueryParameters
        {
            Include = "Orders",
            Expand = "Orders(take=3; filter=Status=\"Delivered\"; sort=OrderDate DESC)",
            Filter = "Id = 1",
            Sort = "Id ASC"
        };

        var result = await ((DbConnection)Connection).FlexQueryAsync<Customer>(parameters, opt =>
        {
            opt.QuerySyntax = QuerySyntax.Fql;
            opt.UseModel(SharedFlexQueryModel.Instance);
        });

        dynamic customer = result.Data.Single();
        var orders = ((IEnumerable<object>)customer.Orders).Cast<dynamic>().ToList();

        orders.Should().HaveCount(3);
        var orderIds = orders.Select(order => (int)order.Id).ToList();
        orderIds.Should().Equal(9305, 9304, 9303);

        foreach(var order in orders)
        {
            ((string)order.Status).Should().Be("Delivered");
            ((object?)order.GetType().GetProperty("Customer")).Should().BeNull();
            ((object?)order.GetType().GetProperty("OrderItems")).Should().BeNull();
        }
    }
}
