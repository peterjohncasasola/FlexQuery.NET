using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Tests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace FlexQuery.NET.Tests.Api.Dapper;

public class OrderAggregationTests : DapperApiTestBase
{
    protected override ISqlDialect Dialect => new SqliteDialect();

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
        items.Should().HaveCount(1);
        
        var first = items[0];
        var keys = string.Join(", ", first.EnumerateObject().Select(p => p.Name));
        first.TryGetProperty("SUM_total", out _).Should().BeTrue($"Keys found: {keys}");
        first.GetProperty("SUM_total").GetDecimal().Should().BeGreaterThan(0);
        first.GetProperty("COUNT_id").GetInt32().Should().BeGreaterThan(0);
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
