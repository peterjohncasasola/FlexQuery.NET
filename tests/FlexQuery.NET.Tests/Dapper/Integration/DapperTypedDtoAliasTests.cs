using System.Data;
using System.Text.Json.Nodes;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared;
using FlexQuery.NET.Tests.Shared.Fixtures;
using FlexQuery.NET.Tests.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FlexQuery.NET.Tests.Dapper.Integration;

/// <summary>
/// Dapper regression tests for typed DTO select-alias projection. Mirrors the EF Core
/// TypedDtoAliasRegressionTests: explicit select controls the public surface and an
/// <c>as</c> alias is an output name that does not require a matching TResponse property.
/// </summary>
public class DapperTypedDtoAliasTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public DapperTypedDtoAliasTests()
    {
        var ctx = SqlProjectionDbContext.CreateSeeded();
        _connection = (SqliteConnection)ctx.Database.GetDbConnection();
        if (_connection.State != ConnectionState.Open) _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    public class AliasOnlyDto
    {
        public string CustomerFullName { get; set; } = string.Empty;
    }

    public class IdNameAliasDto
    {
        public int CustomerId { get; set; }
        public string CustomerFullName { get; set; } = string.Empty;
    }

    public class RichLeakDto
    {
        public int CustomerId { get; set; }
        public string CustomerFullName { get; set; } = string.Empty;
        public int CustomerGroupId { get; set; }
        public DateTime AccountOpenedDate { get; set; }
        public decimal DiscountPercentage { get; set; }
        public bool IsOnCreditHold { get; set; }
    }

    private static JsonObject SerializeFirst<T>(QueryResult<T> result)
        => JsonNode.Parse(FlexQueryTestJson.Serialize(result))!["data"]![0]!.AsObject();

    [Fact]
    public async Task ExplicitSelect_ReturnsOnlySelectedField()
    {
        var parameters = new FlexQueryParameters { Select = "CustomerFullName", Filter = "CustomerFullName:eq:Alice Johnson" };

        var result = await _connection.FlexQueryAsync<Customer, AliasOnlyDto>(parameters, opt =>
        {
            opt.UseModel(SharedFlexQueryModel.Instance);
            opt.MapField<AliasOnlyDto, Customer, string>(d => d.CustomerFullName, e => e.Name);
        });

        result.Data.Should().ContainSingle();
        var item = SerializeFirst(result);
        item.Count.Should().Be(1);
        item.ContainsKey("customerFullName").Should().BeTrue();
        item["customerFullName"]!.GetValue<string>().Should().Be("Alice Johnson");
    }

    [Fact]
    public async Task Alias_WorksWithoutMatchingClrProperty()
    {
        var parameters = new FlexQueryParameters { Select = "CustomerFullName as CustomerName", Filter = "CustomerFullName:eq:Alice Johnson" };

        var result = await _connection.FlexQueryAsync<Customer, AliasOnlyDto>(parameters, opt =>
        {
            opt.UseModel(SharedFlexQueryModel.Instance);
            opt.MapField<AliasOnlyDto, Customer, string>(d => d.CustomerFullName, e => e.Name);
        });

        result.Data.Should().ContainSingle();
        var item = SerializeFirst(result);
        item.ContainsKey("customerName").Should().BeTrue();
        item.ContainsKey("customerFullName").Should().BeFalse();
        item["customerName"]!.GetValue<string>().Should().Be("Alice Johnson");
    }

    [Fact]
    public async Task MultipleSelectFields_WithAlias()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "CustomerId,CustomerFullName as CustomerName",
            Filter = "CustomerId:eq:1"
        };

        var result = await _connection.FlexQueryAsync<Customer, IdNameAliasDto>(parameters, opt =>
        {
            opt.UseModel(SharedFlexQueryModel.Instance);
            opt.MapField<IdNameAliasDto, Customer, int>(d => d.CustomerId, e => e.Id);
            opt.MapField<IdNameAliasDto, Customer, string>(d => d.CustomerFullName, e => e.Name);
        });

        result.Data.Should().ContainSingle();
        var item = SerializeFirst(result);
        item.Count.Should().Be(2);
        item.ContainsKey("customerId").Should().BeTrue();
        item.ContainsKey("customerName").Should().BeTrue();
        item.ContainsKey("customerFullName").Should().BeFalse();
        item["customerId"]!.GetValue<int>().Should().Be(1);
        item["customerName"]!.GetValue<string>().Should().Be("Alice Johnson");
    }

    [Fact]
    public async Task UnselectedDefaults_DoNotLeak()
    {
        var parameters = new FlexQueryParameters { Select = "CustomerFullName", Filter = "CustomerFullName:eq:Alice Johnson" };

        var result = await _connection.FlexQueryAsync<Customer, RichLeakDto>(parameters, opt =>
        {
            opt.UseModel(SharedFlexQueryModel.Instance);
            opt.MapField<RichLeakDto, Customer, int>(d => d.CustomerId, e => e.Id);
            opt.MapField<RichLeakDto, Customer, string>(d => d.CustomerFullName, e => e.Name);
        });

        result.Data.Should().ContainSingle();
        var item = SerializeFirst(result);
        item.ContainsKey("customerFullName").Should().BeTrue();
        item.ContainsKey("customerId").Should().BeFalse();
        item.ContainsKey("customerGroupId").Should().BeFalse();
        item.ContainsKey("accountOpenedDate").Should().BeFalse();
        item.ContainsKey("discountPercentage").Should().BeFalse();
        item.ContainsKey("isOnCreditHold").Should().BeFalse();
    }

    [Fact]
    public async Task FilterAlias_NoMatchingRow_ReturnsEmpty()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "CustomerFullName as CustomerName",
            Filter = "CustomerFullName:eq:Does Not Exist"
        };

        var result = await _connection.FlexQueryAsync<Customer, AliasOnlyDto>(parameters, opt =>
        {
            opt.UseModel(SharedFlexQueryModel.Instance);
            opt.MapField<AliasOnlyDto, Customer, string>(d => d.CustomerFullName, e => e.Name);
        });

        result.Data.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task FilterAlias_ExistingRow_ExposedViaAlias()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "CustomerFullName as CustomerName",
            Filter = "CustomerFullName:eq:Alice Johnson"
        };

        var result = await _connection.FlexQueryAsync<Customer, AliasOnlyDto>(parameters, opt =>
        {
            opt.UseModel(SharedFlexQueryModel.Instance);
            opt.MapField<AliasOnlyDto, Customer, string>(d => d.CustomerFullName, e => e.Name);
        });

        result.Data.Should().ContainSingle();
        var item = SerializeFirst(result);
        item["customerName"]!.GetValue<string>().Should().Be("Alice Johnson");
    }
}



