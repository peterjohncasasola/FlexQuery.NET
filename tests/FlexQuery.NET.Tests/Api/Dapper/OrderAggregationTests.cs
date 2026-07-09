using System.Net.Http.Json;
using System.Text.Json;

namespace FlexQuery.NET.Tests.Api.Dapper;

public class OrderAggregationTests : DapperApiTestBase
{
    public OrderAggregationTests() { }

    [Fact]
    public async Task Should_Group_Orders_By_Customer()
    {
        // Act
        var response = await Client.GetAsync("/api/orders?groupBy=customerId");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = json.GetProperty("Data").EnumerateArray().ToList();
        items.Should().HaveCount(3); // Alice, Bob, BobTwo all have orders
    }

    [Fact]
    public async Task Should_Apply_Aggregates()
    {
        // Act
        var response = await Client.GetAsync("/api/orders?select=sum(total),count(id)");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = json.GetProperty("Data").EnumerateArray().ToList();
        items.Should().HaveCount(4); // Returns all orders since GroupBy is empty
        
        var aggregates = json.GetProperty("Aggregates");
        aggregates.GetProperty("total").GetProperty("sum").GetDecimal().Should().BeGreaterThan(0);
        aggregates.GetProperty("id").GetProperty("count").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Should_Apply_Having_Clause()
    {
        // Act - Group by customer and only return those with total sum > 100
        var response = await Client.GetAsync("/api/orders?groupBy=customerId&having=count(id):gt:1&select=customerId,count(id)");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = json.GetProperty("Data").EnumerateArray().ToList();
        items.Should().HaveCount(1); // Only Alice has > 100 total
    }
}
