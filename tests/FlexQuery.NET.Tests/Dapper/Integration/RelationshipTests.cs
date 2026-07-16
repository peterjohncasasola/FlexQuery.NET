using System.Net.Http.Json;
using System.Text.Json;

namespace FlexQuery.NET.Tests.Dapper.Integration;

public class RelationshipTests : DapperApiTestBase
{
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
        items.Should().HaveCount(3); // Alice, Bob, Carol all have orders > 100
        items.Select(x => x.GetProperty("Name").GetString()).Should().BeEquivalentTo("Alice Johnson", "Bob Smith", "Carol White");
    }

    [Fact]
    public async Task Should_Use_NotExists_For_All_Filter()
    {
        // Act - Users where all orders have total > 10
        var response = await Client.GetAsync("/api/users?filter=orders.all(total:gt:10)");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = json.GetProperty("Data").EnumerateArray().ToList();
        items.Should().Contain(x => x.GetProperty("Name").GetString() == "Alice Johnson");
        items.Should().Contain(x => x.GetProperty("Name").GetString() == "Bob Smith");
        items.Should().Contain(x => x.GetProperty("Name").GetString() == "Carol White");
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
        items[0].GetProperty("Name").GetString().Should().Be("Alice Johnson");
    }
}
