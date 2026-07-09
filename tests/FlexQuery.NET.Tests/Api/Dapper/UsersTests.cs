using System.Net.Http.Json;
using System.Text.Json;

namespace FlexQuery.NET.Tests.Api.Dapper;

public class UsersTests : DapperApiTestBase
{
    public UsersTests() { }

    [Fact]
    public async Task Should_Return_Healthy()
    {
        var response = await Client.GetAsync("/api/users/health");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Healthy");
    }

    [Fact]
    public async Task Should_Filter_Users_By_Name()
    {
        // Act
        var response = await Client.GetAsync("/api/users?filter=name:eq:Alice");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = json.GetProperty("Data").EnumerateArray();
        items.Should().HaveCount(1);
        items.First().GetProperty("Name").GetString().Should().Be("Alice");
    }

    [Fact]
    public async Task Should_Sort_Users_By_Name_Descending()
    {
        // Act
        var response = await Client.GetAsync("/api/users?sort=name:desc");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = json.GetProperty("Data").EnumerateArray().ToList();
        items.Should().HaveCountGreaterThan(1);
        items[0].GetProperty("Name").GetString().Should().Be("Bob"); // "Bob" comes after "Alice"
    }

    [Fact]
    public async Task Should_Apply_Pagination()
    {
        // Act
        var response = await Client.GetAsync("/api/users?page=1&pageSize=1");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("Data").EnumerateArray().Should().HaveCount(1);
        json.GetProperty("TotalCount").GetInt32().Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task Should_Project_Selected_Fields()
    {
        // Act
        var response = await Client.GetAsync("/api/users?select=id,name");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var firstItem = json.GetProperty("Data").EnumerateArray().First();
        
        firstItem.TryGetProperty("Id", out _).Should().BeTrue();
        firstItem.TryGetProperty("Name", out _).Should().BeTrue();
        firstItem.TryGetProperty("Email", out _).Should().BeFalse();
    }
}
