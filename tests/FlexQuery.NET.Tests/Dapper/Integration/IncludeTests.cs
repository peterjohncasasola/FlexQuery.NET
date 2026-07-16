using System.Net.Http.Json;
using System.Text.Json;

namespace FlexQuery.NET.Tests.Dapper.Integration;

public class IncludeTests : DapperApiTestBase
{
    public IncludeTests() { }

    [Fact]
    public async Task Should_Apply_LeftJoin_For_Include()
    {
        // Act
        var response = await Client.GetAsync("/api/users?include=orders");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var alice = json.GetProperty("Data").EnumerateArray()
            .First(x => x.GetProperty("Name").GetString() == "Alice Johnson");
        
        alice.TryGetProperty("Orders", out var orders).Should().BeTrue();
        orders.EnumerateArray().Should().NotBeEmpty();
    }

    [Fact(Skip = "Filtered includes will be supported by the future expand feature")]
    public async Task Should_Apply_Filtered_Include()
    {
        // Act - Only include orders with total > 100
        var response = await Client.GetAsync("/api/users?include=orders(total:gt:100)");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var alice = json.GetProperty("Data").EnumerateArray()
            .First(x => x.GetProperty("Name").GetString() == "Alice Johnson");
        
        var orders = alice.GetProperty("Orders").EnumerateArray().ToList();
        orders.Should().HaveCount(1);
        orders[0].GetProperty("Total").GetDecimal().Should().BeGreaterThan(100);
    }
}
