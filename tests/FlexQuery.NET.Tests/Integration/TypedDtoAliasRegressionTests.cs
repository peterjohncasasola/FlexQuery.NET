using System.Text.Json.Nodes;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.EntityFrameworkCore.Options;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared;
using FlexQuery.NET.Tests.Shared.Models;

namespace FlexQuery.NET.Tests.Integration;

/// <summary>
/// Regression tests for typed DTO select-alias projection and filter-before-projection
/// semantics in FlexQueryAsync&lt;TEntity, TResponse&gt;.
///
/// Invariants:
/// - Explicit select defines the public result surface (only selected fields are serialized).
/// - An <c>as</c> alias is an output name and does NOT require a matching TResponse property.
/// - Source field, TResponse property, and output alias are kept distinct.
/// - Filtering determines row existence before projection.
/// </summary>
public class TypedDtoAliasRegressionTests : IDisposable
{
    private readonly TestDbContext _db = TestDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    // DTOs ------------------------------------------------------------------

    /// <summary>No CustomerName property — proves the alias is output-only.</summary>
    public class AliasOnlyDto
    {
        public string CustomerFullName { get; set; } = string.Empty;
    }

    public class IdNameAliasDto
    {
        public int CustomerId { get; set; }
        public string CustomerFullName { get; set; } = string.Empty;
    }

    /// <summary>Has extra, unselected properties that must not leak into the output.</summary>
    public class RichLeakDto
    {
        public int CustomerId { get; set; }
        public string CustomerFullName { get; set; } = string.Empty;
        public int CustomerGroupId { get; set; }
        public DateTime AccountOpenedDate { get; set; }
        public decimal DiscountPercentage { get; set; }
        public bool IsOnCreditHold { get; set; }
    }

    // Helpers ----------------------------------------------------------------

    private static JsonObject SerializeFirst<T>(QueryResult<T> result)
    {
        var json = JsonNode.Parse(FlexQueryTestJson.Serialize(result))!;
        return json["data"]![0]!.AsObject();
    }

    private static JsonArray SerializeData<T>(QueryResult<T> result)
        => JsonNode.Parse(FlexQueryTestJson.Serialize(result))!["data"]!.AsArray();

    // Tests ------------------------------------------------------------------

    [Fact]
    public async Task ExplicitSelect_ReturnsOnlySelectedField()
    {
        var parameters = new FlexQueryParameters { Select = "CustomerFullName", Filter = "CustomerFullName:eq:Alice Johnson" };

        var result = await _db.Customers.FlexQueryAsync<Customer, AliasOnlyDto>(parameters, opt =>
            opt.MapField<AliasOnlyDto, Customer, string>(d => d.CustomerFullName, e => e.Name));

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

        var result = await _db.Customers.FlexQueryAsync<Customer, AliasOnlyDto>(parameters, opt =>
            opt.MapField<AliasOnlyDto, Customer, string>(d => d.CustomerFullName, e => e.Name));

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

        var result = await _db.Customers.FlexQueryAsync<Customer, IdNameAliasDto>(parameters, opt =>
        {
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

        var result = await _db.Customers.FlexQueryAsync<Customer, RichLeakDto>(parameters, opt =>
        {
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

        var result = await _db.Customers.FlexQueryAsync<Customer, AliasOnlyDto>(parameters, opt =>
            opt.MapField<AliasOnlyDto, Customer, string>(d => d.CustomerFullName, e => e.Name));

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

        var result = await _db.Customers.FlexQueryAsync<Customer, AliasOnlyDto>(parameters, opt =>
            opt.MapField<AliasOnlyDto, Customer, string>(d => d.CustomerFullName, e => e.Name));

        result.Data.Should().ContainSingle();
        var item = SerializeFirst(result);
        item["customerName"]!.GetValue<string>().Should().Be("Alice Johnson");
    }

    [Fact]
    public async Task ServerSideProjection_OnlySelectedSourceColumnsBound()
    {
        // No AsEnumerable / client-side shaping: the selected source property is populated
        // while unselected TResponse properties remain at their CLR defaults.
        var parameters = new FlexQueryParameters
        {
            Select = "CustomerFullName as CustomerName",
            Filter = "CustomerId:eq:1"
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, RichLeakDto>(parameters, opt =>
        {
            opt.MapField<RichLeakDto, Customer, int>(d => d.CustomerId, e => e.Id);
            opt.MapField<RichLeakDto, Customer, string>(d => d.CustomerFullName, e => e.Name);
        });

        result.Data.Should().ContainSingle();
        // The selected source property is materialized on the typed TResponse.
        result.Data[0].CustomerFullName.Should().Be("Alice Johnson");
        // Unselected TResponse properties are not populated by the projection.
        result.Data[0].CustomerGroupId.Should().Be(0);
    }
}



