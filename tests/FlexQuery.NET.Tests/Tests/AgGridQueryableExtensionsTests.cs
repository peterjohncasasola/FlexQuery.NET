using System.Text.Json;
using FlexQuery.NET.Adapters.AgGrid.Models;
using FlexQuery.NET.Adapters.AgGrid.Parsers;
using FlexQuery.NET.EFCore;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Tests.Fixtures;
using FlexQuery.NET.Tests.Models;
using FluentAssertions;

namespace FlexQuery.NET.Tests.Tests;

public class AgGridQueryableExtensionsTests
{
    [Fact]
    public async Task FlexQueryAsync_AgGridRequest_AppliesFilterAndSort()
    {
        using var db = TestDbContext.CreateSeeded("aggrid-filter-sort");
        var request = new AgGridRequest
        {
            FilterModel = new Dictionary<string, AgGridFilterNode>(StringComparer.OrdinalIgnoreCase)
            {
                ["City"] = new()
                {
                    FilterType = "text",
                    Type = "equals",
                    Filter = Element("\"New York\"")
                }
            },
            SortModel =
            [
                new() { ColId = "Age", Sort = "desc" }
            ]
        };

        // Step 1: Parse AG Grid request into QueryOptions (adapter)
        var options = AgGridQueryOptionsParser.Parse(request);

        // Step 2: Execute via EF Core
        var result = await db.Entities.AsQueryable().FlexQueryAsync(options);

        var data = result.Data.Cast<TestEntity>().ToList();
        data.Select(x => x.Id).Should().Equal(8, 3, 1);
        data.Select(x => x.Age).Should().Equal(45, 35, 30);
    }

    [Fact]
    public async Task FlexQueryAsync_AgGridRequest_UsesExecutionOptionsForValidation()
    {
        using var db = TestDbContext.CreateSeeded("aggrid-validation");
        var request = new AgGridRequest
        {
            FilterModel = new Dictionary<string, AgGridFilterNode>(StringComparer.OrdinalIgnoreCase)
            {
                ["MissingField"] = new()
                {
                    FilterType = "text",
                    Type = "equals",
                    Filter = Element("\"New York\"")
                }
            }
        };

        // Step 1: Parse AG Grid request into QueryOptions (adapter)
        var options = AgGridQueryOptionsParser.Parse(request);

        // Step 2: Execute via EF Core — should throw validation error
        var act = async () => await db.Entities.AsQueryable().FlexQueryAsync(options);

        await act.Should().ThrowAsync<QueryValidationException>();
    }

    private static JsonElement Element(string json)
    {
        return JsonDocument.Parse(json).RootElement;
    }
}
