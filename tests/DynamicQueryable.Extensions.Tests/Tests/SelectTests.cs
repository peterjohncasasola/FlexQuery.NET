using DynamicQueryable.Extensions;
using DynamicQueryable.Models;
using DynamicQueryable.Tests.Fixtures;
using DynamicQueryable.Tests.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DynamicQueryable.Tests.Tests;

public class SelectTests : IDisposable
{
    private readonly TestDbContext _db = TestDbContext.CreateSeeded();
    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Select_Empty_ReturnsOriginalTypePropertiesAsDynamic()
    {
        var options = new QueryOptions();
        var query = _db.Entities.ApplySelect(options);
        
        var list = await query.ToListAsync();
        list.Should().NotBeEmpty();
        
        var first = list.First();
        var type = first.GetType();
        
        // Should have primitive properties projected
        type.GetProperty("Id").Should().NotBeNull();
        type.GetProperty("Name").Should().NotBeNull();
        type.GetProperty("Age").Should().NotBeNull();
        
        // Since Select was empty, ApplySelect returns the original query cast to object,
        // so we get the full TestEntity back including Profile.
        type.GetProperty("Profile").Should().NotBeNull();
    }

    [Fact]
    public async Task Select_FlatFields_ProjectsOnlyRequestedFields()
    {
        var options = new QueryOptions { Select = ["Id", "Name"] };
        var query = _db.Entities.ApplySelect(options);
        
        var list = await query.ToListAsync();
        var first = list.First();
        var type = first.GetType();

        type.GetProperty("Id").Should().NotBeNull();
        type.GetProperty("Name").Should().NotBeNull();
        type.GetProperty("Age").Should().BeNull(); // Excluded
        
        // Assert values mapped correctly
        ((int)type.GetProperty("Id")!.GetValue(first)!).Should().Be(1);
        ((string)type.GetProperty("Name")!.GetValue(first)!).Should().Be("Alice Johnson");
    }

    [Fact]
    public async Task Select_NestedProperties_ResolvesAndProjects()
    {
        var options = new QueryOptions { Select = ["Id", "Profile.Bio"] };
        var query = _db.Entities.ApplySelect(options);
        
        var list = await query.ToListAsync();
        var first = list.First();
        var type = first.GetType();

        type.GetProperty("Id").Should().NotBeNull();
        var profileProp = type.GetProperty("Profile");
        profileProp.Should().NotBeNull();

        var profileObj = profileProp!.GetValue(first);
        profileObj.Should().NotBeNull();
        
        var profileType = profileObj!.GetType();
        profileType.GetProperty("Bio").Should().NotBeNull();
        ((string)profileType.GetProperty("Bio")!.GetValue(profileObj)!).Should().Be("Developer");
    }

    [Fact]
    public async Task Select_NestedProperties_HandlesNullRelationsGracefully()
    {
        // Diana Prince (Id=4) has null Profile
        var options = new QueryOptions { Select = ["Id", "Profile.Bio"] };
        var query = _db.Entities.Where(e => e.Id == 4).ApplySelect(options);
        
        var list = await query.ToListAsync();
        var first = list.First();
        
        var profileObj = first.GetType().GetProperty("Profile")!.GetValue(first);
        profileObj.Should().BeNull(); // The null condition expression handles it
    }

    [Fact]
    public async Task Select_CollectionProperties_ProjectsCollectionElements()
    {
        var options = new QueryOptions { Select = ["Id", "Orders.Total"] };
        var query = _db.Entities.Where(e => e.Id == 1).ApplySelect(options);
        
        var list = await query.ToListAsync();
        var first = list.First();
        var type = first.GetType();

        var ordersProp = type.GetProperty("Orders");
        ordersProp.Should().NotBeNull();

        var ordersList = ordersProp!.GetValue(first) as System.Collections.IEnumerable;
        ordersList.Should().NotBeNull();
        
        var items = ordersList!.Cast<object>().ToList();
        items.Should().HaveCount(2);

        var orderType = items[0].GetType();
        orderType.GetProperty("Total").Should().NotBeNull();
        orderType.GetProperty("Id").Should().BeNull(); // Not requested
        
        ((decimal)orderType.GetProperty("Total")!.GetValue(items[0])!).Should().Be(50.0m);
    }

    [Fact]
    public async Task Select_IncludeFormat_BringsInWholeNestedObject()
    {
        var options = new QueryOptions { Includes = ["Profile"] };
        var query = _db.Entities.ApplySelect(options);
        
        var list = await query.ToListAsync();
        var first = list.First();
        var type = first.GetType();

        // Because Select is empty, ProjectionBuilder was called with just "Profile" from Includes.
        // It projects a dynamic type with ONLY the "Profile" property!
        type.GetProperty("Id").Should().BeNull();
        
        var profileProp = type.GetProperty("Profile");
        profileProp.Should().NotBeNull();
        
        var profileObj = profileProp!.GetValue(first);
        var profileType = profileObj!.GetType();
        profileType.GetProperty("Bio").Should().NotBeNull();
    }

    [Fact]
    public async Task Select_JsonTree_BuildsComplexProjection()
    {
        var json = """
        {
          "Id": true,
          "Profile": { "Bio": true },
          "Orders": { "Total": true }
        }
        """;
        
        var selectTree = Helpers.SelectTreeBuilder.ParseJsonSelect(JsonDocument.Parse(json).RootElement);
        var options = new QueryOptions { SelectTree = selectTree };
        
        var query = _db.Entities.Where(e => e.Id == 1).ApplySelect(options);
        var list = await query.ToListAsync();
        var first = list.First();
        var type = first.GetType();

        type.GetProperty("Id").Should().NotBeNull();
        type.GetProperty("Profile").Should().NotBeNull();
        type.GetProperty("Orders").Should().NotBeNull();
        
        var profileType = type.GetProperty("Profile")!.GetValue(first)!.GetType();
        profileType.GetProperty("Bio").Should().NotBeNull();
        
        var orders = ((System.Collections.IEnumerable)type.GetProperty("Orders")!.GetValue(first)!).Cast<object>().ToList();
        orders[0].GetType().GetProperty("Total").Should().NotBeNull();
    }

    [Fact]
    public async Task Select_InvalidField_IsIgnoredAndDoesNotThrow()
    {
        var options = new QueryOptions { Select = ["Id", "NonExistentField", "Profile.Fake"] };
        var query = _db.Entities.ApplySelect(options);
        
        var list = await query.ToListAsync();
        var first = list.First();
        var type = first.GetType();

        type.GetProperty("Id").Should().NotBeNull();
        type.GetProperty("NonExistentField").Should().BeNull();
        
        var profileProp = type.GetProperty("Profile");
        profileProp.Should().NotBeNull();
        profileProp!.GetValue(first)!.GetType().GetProperty("Fake").Should().BeNull();
    }
}
