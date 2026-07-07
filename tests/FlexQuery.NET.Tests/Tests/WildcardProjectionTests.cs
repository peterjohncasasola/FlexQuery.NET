using FlexQuery.NET;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Tests.Fixtures;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FlexQuery.NET.Tests.Tests;

public class WildcardProjectionTests : IDisposable
{
    private readonly SqlProjectionDbContext _db = SqlProjectionDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ApplySelect_WithWildcard_IncludesAllScalars()
    {
        // Arrange
        var options = new QueryOptions();
        options.Select = new List<string> { "Id", "Orders.*" };

        // Act
        var result = await _db.Customers
            .AsNoTracking()
            .Apply(options)
            .ApplySelect(options)
            .ToListAsync();

        // Assert
        result.Should().NotBeEmpty();
        var alice = result.First(c => (int)c.GetType().GetProperty("Id")?.GetValue(c)! == 1);
        
        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        var orderList = orders!.Cast<object>().ToList();
        
        orderList.Should().NotBeEmpty();
        var order = orderList[0];
        
        // Should have all scalars of SqlOrder
        order.GetType().GetProperty("Number").Should().NotBeNull();
        order.GetType().GetProperty("Total").Should().NotBeNull();
        order.GetType().GetProperty("OrderDate").Should().NotBeNull();
        
        // Should NOT have navigations (unless specified)
        order.GetType().GetProperty("Items").Should().BeNull();
        order.GetType().GetProperty("Customer").Should().BeNull();
    }

    [Fact]
    public async Task ApplySelect_WithDeepWildcard_IncludesNestedScalars()
    {
        // Arrange
        var options = new QueryOptions();
        // Alice -> Orders -> Items (all scalars)
        options.Select = new List<string> { "Id", "Orders.Number", "Orders.Items.*" };

        // Act
        var result = await _db.Customers
            .AsNoTracking()
            .Apply(options)
            .ApplySelect(options)
            .ToListAsync();

        // Assert
        result.Should().NotBeEmpty();
        var alice = result.First(c => (int)c.GetType().GetProperty("Id")?.GetValue(c)! == 1);
        
        var orders = alice.GetType().GetProperty("Orders")?.GetValue(alice) as System.Collections.IEnumerable;
        var order = orders!.Cast<object>().First(o => (string)o.GetType().GetProperty("Number")?.GetValue(o)! == "SO-001");
        
        var items = order.GetType().GetProperty("Items")?.GetValue(order) as System.Collections.IEnumerable;
        var itemList = items!.Cast<object>().ToList();
        
        itemList.Should().NotBeEmpty();
        var item = itemList[0];
        
        // Should have Sku (scalar)
        item.GetType().GetProperty("Sku").Should().NotBeNull();
        
        // Should NOT have Order (navigation)
        item.GetType().GetProperty("Order").Should().BeNull();
    }

    [Fact]
    public async Task DefaultProjection_WhenSelectableFieldsSet_AppliesWhenNoSelectProvided()
    {
        // Arrange
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            SelectableFields = new HashSet<string> { "Id", "Name" }
        };

        // Act
        options.ValidateOrThrow<SqlCustomer>(execOptions);
        var result = await _db.Customers
            .AsNoTracking()
            .Apply(options)
            .ApplySelect(options) 
            .ToListAsync();

        // Assert
        result.Should().NotBeEmpty();
        var first = result[0];
        
        // Should have Id and Name
        first.GetType().GetProperty("Id").Should().NotBeNull();
        first.GetType().GetProperty("Name").Should().NotBeNull();
        
        // Should NOT have Email (since it wasn't in SelectableFields)
        first.GetType().GetProperty("Email").Should().BeNull();
    }

    [Fact]
    public async Task StrictFieldValidation_Throws_OnForbiddenField()
    {
        // Arrange
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            SelectableFields = new HashSet<string> { "Id" },
            StrictFieldValidation = true
        };
        options.Select = new List<string> { "Id", "Name" }; // Name is forbidden

        // Act
        var act = () => options.ValidateOrThrow<SqlCustomer>(execOptions);

        // Assert
        act.Should().Throw<QueryValidationException>()
           .WithMessage("*'Name' is not allowed*");
    }

    [Fact]
    public async Task FilterableFields_WithWildcard_AllowsNestedPaths()
    {
        // Arrange
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            FilterableFields = new HashSet<string> { "Id", "Orders.*" },
            StrictFieldValidation = true
        };
        options.Filter = new FilterGroup 
        { 
            Filters = new List<FilterCondition> 
            { 
                new() { Field = "Orders.Total", Operator = "gt", Value = "100" } 
            } 
        };

        // Act
        var act = () => options.ValidateOrThrow<SqlCustomer>(execOptions);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task SortableFields_Blocks_NonWhitelistedField()
    {
        // Arrange
        var options = new QueryOptions();
        var execOptions = new QueryExecutionOptions
        {
            SortableFields = new HashSet<string> { "Id" },
            StrictFieldValidation = true
        };
        options.Sort.Add(new SortNode { Field = "Name" }); // Name not in whitelist

        // Act
        var act = () => options.ValidateOrThrow<SqlCustomer>(execOptions);

        // Assert
        act.Should().Throw<QueryValidationException>()
           .WithMessage("*'Name' is not allowed for Sort*");
    }
}
