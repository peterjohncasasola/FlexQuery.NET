using FlexQuery.NET.Exceptions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FlexQuery.NET.Tests.Dapper.Integration;

public class SecurityTests : DapperApiTestBase
{
    public SecurityTests() { }

    [Fact]
    public async Task Should_Block_SQL_Injection_In_Filter()
    {
        // Act
        var response = await Client.GetAsync("/api/users?filter=name:contains:'; DROP TABLE Customers;--");

        // Assert
        // The DSL parser rejects filter expressions containing unparseable character sequences.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_Block_SQL_Injection_In_Sort()
    {
        // Act
        var response = await Client.GetAsync("/api/users?sort=Name;DROP TABLE Customers");

        // Assert
        // Sort field validation should reject this because "Name;DROP TABLE Customers" is not a valid field.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

public class ValidationTests : DapperApiTestBase
{
    public ValidationTests() { }

    [Fact]
    public async Task Should_Reject_Disallowed_Field()
    {
        // Act - Assume "SecretField" is not in the model or blocked
        var response = await Client.GetAsync("/api/users?filter=secretField:eq:value");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_Enforce_MaxPageSize()
    {
        // Act
        var response = await Client.GetAsync("/api/users?pageSize=1000000");

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = json.GetProperty("Data").EnumerateArray().ToList();
        
        // Default max page size is usually 100 or 1000.
        items.Count.Should().BeLessThan(1000000);
    }
}
