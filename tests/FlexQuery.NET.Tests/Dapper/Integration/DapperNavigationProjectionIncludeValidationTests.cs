using System.Data;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FlexQuery.NET.Tests.Dapper.Integration;

/// <summary>
/// Dapper parity for the navigation-projection include-authorization contract.
/// Validation is shared with the EF Core pipeline; these tests prove identical
/// outcomes through the Dapper execution path.
/// </summary>
public class DapperNavigationProjectionIncludeValidationTests : IDisposable
{
    private readonly SqlProjectionDbContext _db;
    private readonly SqliteConnection _connection;

    public DapperNavigationProjectionIncludeValidationTests()
    {
        _db = SqlProjectionDbContext.CreateSeeded();
        _connection = (SqliteConnection)_db.Database.GetDbConnection();
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }

    public void Dispose() => _db.Dispose();

    public class CustomerNavDto
    {
        public int Id { get; set; }
        public List<Order>? Orders { get; set; }
    }

    public class CustomerNestedDto
    {
        public int Id { get; set; }
        public List<OrderNestedDto>? Orders { get; set; }
    }

    public class OrderNestedDto
    {
        public int Id { get; set; }
        public List<OrderItemNestedDto>? OrderItems { get; set; }
    }

    public class OrderItemNestedDto
    {
        public int Id { get; set; }
    }

    [Fact]
    public async Task EntityMode_NestedSelect_WithoutInclude_Throws()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "Id,Orders(Id,Status)",
            Filter = "Id:eq:1"
        };

        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _connection.FlexQueryAsync<Customer>(parameters));

        ex.Message.Should().Contain("not included");
        ex.Message.Should().Contain("Orders");
    }

    [Fact]
    public async Task EntityMode_NestedSelect_WithInclude_Passes()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "Id,Orders(Id,Status)",
            Include = "Orders",
            Filter = "Id:eq:1"
        };

        var result = await _connection.FlexQueryAsync<Customer>(parameters);

        result.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task NestedSelect_WithoutInclude_Throws()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "Id,Orders(Id,Status)",
            Filter = "Id:eq:1"
        };

        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _connection.FlexQueryAsync<Customer, CustomerNavDto>(parameters, opt => { }));

        ex.Message.Should().Contain("not included");
        ex.Message.Should().Contain("Orders");
    }

    [Fact]
    public async Task NestedSelect_WithInclude_Passes()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "Id,Orders(Id,Status)",
            Include = "Orders",
            Filter = "Id:eq:1"
        };

        var result = await _connection.FlexQueryAsync<Customer, CustomerNavDto>(parameters, opt => { });

        result.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LenientMode_WithoutInclude_NavigationRemoved()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "Id,Orders(Id,Status)",
            Filter = "Id:eq:1"
        };

        var result = await _connection.FlexQueryAsync<Customer, CustomerNavDto>(parameters, opt =>
            opt.StrictFieldValidation = false);

        result.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task NestedLevels_ChildPathNotIncluded_Throws()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "Id,Orders(Id,OrderItems(Id))",
            Include = "Orders",
            Filter = "Id:eq:1"
        };

        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _connection.FlexQueryAsync<Customer, CustomerNestedDto>(parameters, opt => { }));

        ex.Message.Should().Contain("Orders.OrderItems");
    }
}

