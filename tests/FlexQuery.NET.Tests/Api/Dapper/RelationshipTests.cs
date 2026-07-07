using FlexQuery.NET.Dapper.Dialects;
using System.Net.Http.Json;
using System.Text.Json;

namespace FlexQuery.NET.Tests.Api.Dapper;

public class RelationshipTests : DapperApiTestBase
{
    protected override ISqlDialect Dialect => new SqliteDialect();

    public RelationshipTests() { }

    [Fact]
    public async Task Should_Use_Exists_For_Any_Filter()
    {
        // Act - Users who have any order with total > 100
        var response = await Client.GetAsync("/api/users?filter=orders.any(total:gt:100)");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = json.GetProperty("Data").EnumerateArray().ToList();
        items.Should().HaveCount(1);
        items[0].GetProperty("Name").GetString().Should().Be("Alice");
    }

    [Fact]
    public async Task Should_Use_NotExists_For_All_Filter()
    {
        // Act - Users where all orders have total > 10
        // (Bob has one order with total 99, so he matches. 
        // Alice has one order with 125 and one with 45, so she matches.
        // Carol has no orders, so she technically matches (vacuously true)
        var response = await Client.GetAsync("/api/users?filter=orders.all(total:gt:5)");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = json.GetProperty("Data").EnumerateArray().ToList();
        items.Should().Contain(x => x.GetProperty("Name").GetString() == "Alice");
        items.Should().Contain(x => x.GetProperty("Name").GetString() == "Bob");
    }

    [Fact]
    public async Task Should_Use_Subquery_For_Count_Filter()
    {
        // Act - Users with more than 1 order
        var response = await Client.GetAsync("/api/users?filter=orders.count():gt:1");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = json.GetProperty("Data").EnumerateArray().ToList();
        items.Should().HaveCount(1);
        items[0].GetProperty("Name").GetString().Should().Be("Alice");
    }
}
