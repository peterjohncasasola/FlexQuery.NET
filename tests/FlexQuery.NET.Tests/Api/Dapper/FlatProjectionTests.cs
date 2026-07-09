using System.Net.Http.Json;
using System.Text.Json;

namespace FlexQuery.NET.Tests.Api.Dapper;

public class FlatProjectionTests : DapperApiTestBase
{
    public FlatProjectionTests() { }

    [Fact]
    public async Task FlatMode_WithSingleSelect_ProjectsLeafFieldsOnly()
    {
        var response = await Client.GetAsync("/api/users?mode=flat&select=Orders.Total,Orders.Number as OrderNumber");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = json.GetProperty("Data").EnumerateArray();

        data.Should().NotBeEmpty();

        var firstItem = data.First();
        
        firstItem.TryGetProperty("Total", out _).Should().BeTrue();
        firstItem.TryGetProperty("OrderNumber", out _).Should().BeTrue();
        firstItem.TryGetProperty("Orders", out _).Should().BeFalse("Flat mode should not return nested collections");
    }

    [Fact]
    public async Task FlatMixedMode_WithRootAndNestedFields_ProjectsAllFieldsInSingleRow()
    {
        var response = await Client.GetAsync("/api/users?mode=flat-mixed&select=Name as customerName,Orders.Total,Orders.Number as orderNumber");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = json.GetProperty("Data").EnumerateArray();

        data.Should().NotBeEmpty();

        var firstItem = data.First();
        
        firstItem.TryGetProperty("customerName", out var customerName).Should().BeTrue();
        firstItem.TryGetProperty("Total", out _).Should().BeTrue();
        firstItem.TryGetProperty("orderNumber", out _).Should().BeTrue();

        var names = data.Select(x => x.GetProperty("customerName").GetString()).ToList();
        names.Should().NotContainNulls();
    }

    [Fact]
    public async Task FlatMode_MultiLevelNestedCollection_ProjectsCorrectly()
    {
        var response = await Client.GetAsync("/api/users?mode=flat&select=Orders.Items.Sku,Orders.Items.Id");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = json.GetProperty("Data").EnumerateArray();

        data.Should().NotBeEmpty();

        var firstItem = data.First();
        
        firstItem.TryGetProperty("Sku", out _).Should().BeTrue();
        firstItem.TryGetProperty("Id", out _).Should().BeTrue();
        firstItem.TryGetProperty("Orders", out _).Should().BeFalse();
        firstItem.TryGetProperty("Items", out _).Should().BeFalse();
    }
}