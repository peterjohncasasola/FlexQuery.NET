using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Tests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;

namespace FlexQuery.NET.Tests.Api.Dapper;

public class IncludeTests : DapperApiTestBase
{
    protected override ISqlDialect Dialect => new SqliteDialect();

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
            .First(x => x.GetProperty("Name").GetString() == "Alice");
        
        alice.TryGetProperty("Orders", out var orders).Should().BeTrue();
        orders.EnumerateArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Should_Apply_Filtered_Include()
    {
        // Act - Only include orders with total > 100
        var response = await Client.GetAsync("/api/users?include=orders(total:gt:100)");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var alice = json.GetProperty("Data").EnumerateArray()
            .First(x => x.GetProperty("Name").GetString() == "Alice");
        
        var orders = alice.GetProperty("Orders").EnumerateArray().ToList();
        orders.Should().HaveCount(1);
        orders[0].GetProperty("Total").GetDecimal().Should().BeGreaterThan(100);
    }
}
