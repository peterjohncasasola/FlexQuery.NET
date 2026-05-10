using FlexQuery.NET;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Tests.Fixtures;
using FlexQuery.NET.Tests.Models;
using FluentAssertions;
using System.Text.Json;

namespace FlexQuery.NET.Tests.Tests;

public class DtoMappingTests : IDisposable
{
    private readonly TestDbContext _db = TestDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void FlexQuery_MappedField_Filtering_AppliesMappedExpression()
    {
        var parameters = new FlexQueryParameters
        {
            Filter = "dtoName:eq:Alice Johnson_Mapped"
        };

        var result = _db.Entities.AsQueryable().FlexQuery(parameters, exec =>
        {
            exec.MapField<TestEntity, string>("dtoName", x => x.Name + "_Mapped");
        });

        result.Data.Should().HaveCount(1);
        var json = JsonSerializer.Serialize(result.Data);
        json.Should().Contain("Alice Johnson");
    }

    [Fact]
    public void FlexQuery_MappedField_Sorting_AppliesMappedExpression()
    {
        var parameters = new FlexQueryParameters
        {
            Sort = "dtoAge",
            PageSize = 100
        };

        var result = _db.Entities.AsQueryable().FlexQuery(parameters, exec =>
        {
            // Map dtoAge to (Age * 2)
            exec.MapField<TestEntity, int>("dtoAge", x => x.Age * 2);
        });

        var items = result.Data.Cast<TestEntity>().ToList();
        items.Select(x => x.Age * 2).Should().BeInAscendingOrder();
    }

    [Fact]
    public void FlexQuery_MappedField_Select_ProjectsMappedExpression()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "dtoName, dtoBio",
            Filter = "Name:eq:Alice Johnson"
        };

        // Note: Seeder sets Alice's profile to non-null with bio "Bio for Alice Johnson"
        var result = _db.Entities.AsQueryable().FlexQuery(parameters, exec =>
        {
            exec.MapField<TestEntity, string>("dtoName", x => x.Name);
            exec.MapField<TestEntity, string>("dtoBio", x => x.Profile != null ? x.Profile.Bio : "No Bio");
        });

        result.Data.Should().HaveCount(1);
        
        // Assert JSON to verify dynamic projection
        var json = JsonSerializer.Serialize(result.Data[0]);
        json.Should().Contain("\"dtoName\":\"Alice Johnson\"");
        json.Should().Contain("\"dtoBio\":\"Developer\"");
    }

    [Fact]
    public void FlexQuery_MappedField_Aggregate_AppliesMappedExpression()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "count(dtoAge) as countAge",
            GroupBy = "City",
            PageSize = 100
        };

        var result = _db.Entities.AsQueryable().FlexQuery(parameters, exec =>
        {
            exec.MapField<TestEntity, int>("dtoAge", x => x.Age);
        });

        result.Data.Should().NotBeEmpty();
        var json = JsonSerializer.Serialize(result.Data);
        json.Should().Contain("countAge");
    }

    [Fact]
    public void FlexQuery_MappedField_NestedFilter_AppliesMappedExpression()
    {
        var parameters = new FlexQueryParameters
        {
            // totalAmount = Sum of Order.Total
            Filter = "totalAmount:gt:100"
        };

        var result = _db.Entities.AsQueryable().FlexQuery(parameters, exec =>
        {
            exec.MapField<TestEntity, decimal>("totalAmount", x => x.Orders.Sum(o => o.Total));
        });

        // David Brown has 2 orders (100.0, 50.0) -> sum 150.0
        result.Data.Should().NotBeEmpty();
        var items = result.Data.Cast<TestEntity>().ToList();
        items.Should().AllSatisfy(x => x.Orders.Sum(o => o.Total).Should().BeGreaterThan(100));
    }
}
