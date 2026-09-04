using System.Text.Json.Nodes;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Mapping;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared;
using FlexQuery.NET.Tests.Shared.Fixtures;
using Xunit;

namespace FlexQuery.NET.Tests.Mapping;

/// <summary>
/// Global mapping registry tests: mappings registered once at application startup are
/// automatically reused by every FlexQueryAsync&lt;TEntity, TResponse&gt; call — same-name
/// scalars by convention, renamed scalars via ForMember. Per-query registrations win
/// over global ones. Entity-only names stay rejected.
/// </summary>
[Collection("GlobalMapping")]public class GlobalMappingRegistryTests : IDisposable
{
    private readonly ContestantDbContext _db = ContestantDbContext.Create();

    public GlobalMappingRegistryTests()
    {
        FlexQueryMapping.Configure(registry =>
        {
            registry.GetOrCreate<Contestant, ContestantResponse>();

            var renamed = (TypeMap)registry.GetOrCreate(typeof(Contestant), typeof(RenamedResponse));
            renamed.RegisterMember(new PropertyMap
            {
                DestinationName = nameof(RenamedResponse.ContestantName),
                SourceExpression = (System.Linq.Expressions.Expression<Func<Contestant, string>>)(e => e.Name),
                SourceProperty = typeof(Contestant).GetProperty(nameof(Contestant.Name))!,
                DestinationProperty = typeof(RenamedResponse).GetProperty(nameof(RenamedResponse.ContestantName))!,
                SourceValueType = typeof(string),
                DestinationValueType = typeof(string),
                IsImplicit = false
            });
        });
    }

    public void Dispose() => FlexQueryMapping.Reset();

    public class ContestantResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public decimal Score { get; set; }
    }

    public class RenamedResponse
    {
        public int Id { get; set; }
        public string ContestantName { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    // Global registration without per-query config ---------------------------------

    [Fact]
    public async Task GlobalRegistration_UsedWithoutPerQueryCreateMap()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, ContestantResponse>(
            new FlexQueryParameters { Filter = "Id:eq:1" });

        result.Data.Should().ContainSingle();
        result.Data[0].Name.Should().Be("John");
    }

    [Fact]
    public async Task GlobalRenamedScalar_AllOperationsResolve()
    {
        var selected = await _db.Contestants.FlexQueryAsync<Contestant, RenamedResponse>(
            new FlexQueryParameters { Select = "ContestantName", Filter = "Id:eq:1" });
        selected.Data[0].ContestantName.Should().Be("John");

        var filtered = await _db.Contestants.FlexQueryAsync<Contestant, RenamedResponse>(
            new FlexQueryParameters { Filter = "ContestantName:eq:Jane" });
        filtered.Data.Should().ContainSingle();
        filtered.Data[0].ContestantName.Should().Be("Jane");

        var sorted = await _db.Contestants.FlexQueryAsync<Contestant, RenamedResponse>(
            new FlexQueryParameters { Sort = "ContestantName:asc" });
        sorted.Data.Select(d => d.ContestantName).Should().Equal("Bob", "Jane", "John");

        var grouped = await _db.Contestants.FlexQueryAsync<Contestant, RenamedResponse>(
            new FlexQueryParameters { GroupBy = "ContestantName" });
        grouped.Data.Should().HaveCount(3);
    }

    [Fact]
    public async Task EntityOnlyName_RejectedWithGlobalRegistry()
    {
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Contestants.FlexQueryAsync<Contestant, RenamedResponse>(
                new FlexQueryParameters { Select = "Name" }));

        ex.Message.ToLowerInvariant().Should().Contain("name");
    }

    // Per-query override wins over global -------------------------------------------

    [Fact]
    public async Task PerQueryOverride_WinsOverGlobal()
    {
        var result = await _db.Contestants.FlexQueryAsync<Contestant, RenamedResponse>(
            new FlexQueryParameters { Filter = "ContestantName:eq:John!" },
            opt => opt.CreateMap<Contestant, RenamedResponse>()
                .ForMember(x => x.ContestantName, e => e.Name + "!"));

        result.Data.Should().ContainSingle();
        result.Data[0].ContestantName.Should().Be("John!");
    }
}

/// <summary>
/// Nested navigation via the global registry: same-name collection navigation
/// automatically maps through the registered element map; raw entity graphs never
/// leak into nested DTO graphs. Uses the SharedTestDbContext (Customer→Orders).
/// </summary>
[Collection("GlobalMapping")]
public class GlobalNestedMappingTests : IDisposable
{
    private readonly SharedTestDbContext _db = SharedTestDbContext.CreateInMemorySeeded();

    public void Dispose() => FlexQueryMapping.Reset();

    public class OrderNestedDto
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class CustomerWithOrdersDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<OrderNestedDto> Orders { get; set; } = new();
    }

    [Fact]
    public async Task GlobalNestedNavigation_AutoResolvesWithElementMap()
    {
        FlexQueryMapping.Configure(registry =>
        {
            registry.GetOrCreate<Customer, CustomerWithOrdersDto>();
            registry.GetOrCreate<Order, OrderNestedDto>();
        });

        try
        {
            var result = await _db.Customers.FlexQueryAsync<Customer, CustomerWithOrdersDto>(
                new FlexQueryParameters
                {
                    Filter = "Id:eq:1",
                    Include = "Orders",
                    Select = "Name,Orders(Id,Status)"
                });

            var item = JsonNode.Parse(FlexQueryTestJson.Serialize(result))!["data"]![0]!.AsObject();
            item.ContainsKey("name").Should().BeTrue();

            var orders = item["orders"]!.AsArray();
            orders.Should().NotBeEmpty();
            foreach (var order in orders)
            {
                var obj = order!.AsObject();
                obj.ContainsKey("id").Should().BeTrue();
                obj.ContainsKey("status").Should().BeTrue();
                obj.ContainsKey("total").Should().BeFalse();
                obj.ContainsKey("price").Should().BeFalse();
                obj.ContainsKey("customer").Should().BeFalse();
            }
        }
        finally
        {
            FlexQueryMapping.Reset();
        }
    }

    [Fact]
    public async Task MissingNestedMap_NavigationIsUnmapped_Rejected()
    {
        FlexQueryMapping.Configure(registry =>
        {
            registry.GetOrCreate<Customer, CustomerWithOrdersDto>();
            // deliberately NOT registering Order → OrderNestedDto
        });

        try
        {
            var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
                await _db.Customers.FlexQueryAsync<Customer, CustomerWithOrdersDto>(
                    new FlexQueryParameters
                    {
                        Filter = "Id:eq:1",
                        Include = "Orders",
                        Select = "Name,Orders(Id,Status)"
                    }));

            ex.Message.Should().Contain("Orders");
        }
        finally
        {
            FlexQueryMapping.Reset();
        }
    }
}

