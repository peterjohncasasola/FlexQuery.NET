using FlexQuery.NET.Models;
using System.Text.Json;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Tests.Integration;

public class DtoMappingTests : IDisposable
{
    private readonly TestDbContext _db = TestDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void FlexQuery_MappedField_Filtering_AppliesMappedExpression()
    {
        var parameters = new FlexQueryParameters
        {
            Filter = "dtoName:eq:Alice Johnson_Mapped",
            Select = "dtoName"
        };

        var result = _db.Customers.AsQueryable().FlexQuery(parameters, exec =>
        {
            exec.MapField<Customer, string>("dtoName", x => x.Name + "_Mapped");
        });

        result.Data.Should().HaveCount(1);
        var first = result.Data[0];
        var nameProp = first.GetType().GetProperty("dtoName");
        nameProp.Should().NotBeNull();
        nameProp!.GetValue(first).ToString().Should().Be("Alice Johnson_Mapped");
    }

    [Fact]
    public void FlexQuery_MappedField_Sorting_AppliesMappedExpression()
    {
        var parameters = new FlexQueryParameters
        {
            Sort = "dtoAge",
            PageSize = 100
        };

        var result = _db.Customers.AsQueryable().FlexQuery(parameters, exec =>
        {
            // Map dtoAge to (Age * 2)
            exec.MapField<Customer, int>("dtoAge", x => x.Age * 2);
        });

        var items = result.Data.Cast<Customer>().ToList();
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
        var result = _db.Customers.AsQueryable().FlexQuery(parameters, exec =>
        {
            exec.MapField<Customer, string>("dtoName", x => x.Name);
            exec.MapField<Customer, string>("dtoBio", x => x.Profile != null ? x.Profile.Bio : "No Bio");
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
            Select = "City",
            Aggregate = "count:dtoAge:countAge",
            GroupBy = "City",
            PageSize = 100
        };

        var result = _db.Customers.AsQueryable().FlexQuery(parameters, exec =>
        {
            exec.MapField<Customer, int>("dtoAge", x => x.Age);
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

        var result = _db.Customers.AsQueryable().FlexQuery(parameters, exec =>
        {
            exec.MapField<Customer, decimal>("totalAmount", x => x.Orders.Sum(o => o.Total));
        });

        // David Brown has 2 orders (100.0, 50.0) -> sum 150.0
        result.Data.Should().NotBeEmpty();
        var items = result.Data.Cast<Customer>().ToList();
        items.Should().AllSatisfy(x => x.Orders.Sum(o => o.Total).Should().BeGreaterThan(100));
    }

    [Fact]
    public void FqlQuery_MappedField_NestedSelectSyntax_AppliesMappedExpression()
    {
        FlexQuery.NET.Parsers.Fql.Fql.Register();

        var parameters = new FlexQueryParameters
        {
            Select = "Profile(Bio AS profileBio)"
        };

        var options = QueryOptionsParser.Parse(parameters, QuerySyntax.Fql);

        var result = _db.Customers.AsQueryable().FlexQuery(options);

        result.Data.Should().NotBeEmpty();
        var first = result.Data[0];
        var profileProp = first.GetType().GetProperty("Profile");
        profileProp.Should().NotBeNull();
        var profileObj = profileProp!.GetValue(first);
        profileObj.Should().NotBeNull();
        profileObj.GetType().GetProperty("profileBio").Should().NotBeNull();
        profileObj.GetType().GetProperty("Bio").Should().BeNull();
    }
    
    
    [Fact]
    public void DslQuery_MappedField_NestedSelectSyntax_AppliesMappedExpression()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "Profile(Bio:profileBio)"
        };

        var result = _db.Customers.AsQueryable().FlexQuery(parameters);

        result.Data.Should().NotBeEmpty();
        var first = result.Data[0];
        var profileProp = first.GetType().GetProperty("Profile");
        profileProp.Should().NotBeNull();
        var profileObj = profileProp!.GetValue(first);
        profileObj.Should().NotBeNull();
        profileObj?.GetType().GetProperty("profileBio").Should().NotBeNull();
        profileObj?.GetType().GetProperty("Bio").Should().BeNull();
    }
}
