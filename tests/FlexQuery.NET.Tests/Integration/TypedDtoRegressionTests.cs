using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.EntityFrameworkCore.Options;
using FlexQuery.NET.Models;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Options;
using FlexQuery.NET.Parsers;

namespace FlexQuery.NET.Tests.Integration;

public class TypedDtoRegressionTests : IDisposable
{
    private readonly TestDbContext _db = TestDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    // DTOs used by regression tests

    public class CustomerDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? City { get; set; }
        public int Age { get; set; }
        public Profile? Profile { get; set; }
        public List<Order>? Orders { get; set; }
    }

    public class CustomerNameDto
    {
        public string FullName { get; set; } = string.Empty;
    }

    public class CitySummaryDto
    {
        public int Id { get; set; }
        public string? City { get; set; }
        public int TotalCustomers { get; set; }
    }

    public class CustomerStatsDto
    {
        public int Id { get; set; }
        public decimal Salary { get; set; }
        public int TotalCount { get; set; }
        public decimal TotalSalary { get; set; }
    }

    public class CustomerGroupDto
    {
        public string CustomerName { get; set; } = string.Empty;
    }

    /// <summary>Exposes only the aggregated field — used to prove missing alias properties throw.</summary>
    public class SalaryOnlyDto
    {
        public decimal Salary { get; set; }
    }

    [Fact]
    public async Task FlexQueryAsync_TEntity_TResponse_Filtering_ByDtoFieldName_Works()
    {
        var parameters = new FlexQueryParameters
        {
            Filter = "fullName:eq:Alice Johnson",
            Select = "fullName"
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerNameDto>(parameters, opt =>
        {
            opt.MapField<CustomerNameDto, Customer, string>(
                dto => dto.FullName,
                entity => entity.Name);
        });

        result.Data.Should().HaveCount(1);
        result.Data[0].FullName.Should().Be("Alice Johnson");
    }

    [Fact]
    public async Task FlexQueryAsync_TEntity_TResponse_Sorting_ByDtoFieldName_Works()
    {
        var parameters = new FlexQueryParameters
        {
            Sort = "fullName",
            PageSize = 100
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(parameters, opt =>
        {
            opt.MapField<CustomerDto, Customer, string>(
                dto => dto.FullName,
                entity => entity.Name);
        });

        var names = result.Data.Select(d => d.FullName).ToList();
        names.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task FlexQueryAsync_TEntity_TResponse_Paging_Works()
    {
        var parameters = new FlexQueryParameters
        {
            Page = 1,
            PageSize = 2
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(parameters, opt =>
        {
            opt.MapField<CustomerDto, Customer, string>(
                dto => dto.FullName,
                entity => entity.Name);
        });

        result.Data.Should().HaveCount(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task FlexQueryAsync_TEntity_TResponse_Include_IsPreserved()
    {
        var parameters = new FlexQueryParameters
        {
            Include = "Profile"
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(parameters, opt =>
        {
            opt.MapField<CustomerDto, Customer, string>(
                dto => dto.FullName,
                entity => entity.Name);
        });

        result.Data.Should().NotBeEmpty();
        // The included navigation must actually land on the DTO surface.
        result.Data.Where(d => d.Profile != null).Should().NotBeEmpty();
    }

    [Fact]
    public async Task FlexQueryAsync_TEntity_TResponse_Expand_IsPreserved()
    {
        // Expand may only target collection navigations; Profile is a reference
        // navigation, so the navigation is materialized via include + select tree.
        var parameters = new FlexQueryParameters
        {
            Include = "Profile",
            Select = "FullName,Profile(Bio)"
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(parameters, opt =>
        {
            opt.MapField<CustomerDto, Customer, string>(
                dto => dto.FullName,
                entity => entity.Name);
        });

        result.Data.Should().NotBeEmpty();
        result.Data.Where(d => d.Profile != null).Should().NotBeEmpty();
    }

    [Fact]
    public async Task FlexQueryAsync_TEntity_TResponse_Include_CutsNestedNavigationsFromResponse()
    {
        // The DTO binds the entity collection (entity Orders). Even when the host uses a
        // tracked context (inverse fixup populates Order.Customer), the response must not
        // contain nested navigation graphs: only scalar properties survive the cut.
        var parameters = new FlexQueryParameters
        {
            Include = "Orders"
        };

        var result = await _db.Customers
            .FlexQueryAsync<Customer, CustomerDto>(parameters, opt =>
            {
                opt.MapField<CustomerDto, Customer, string>(
                    dto => dto.FullName,
                    entity => entity.Name);
                opt.UseNoTracking = false;
            });

        result.Data.Should().NotBeEmpty();
        var customersWithOrders = result.Data.Where(d => d.Orders is { Count: > 0 }).ToList();
        customersWithOrders.Should().NotBeEmpty();

        foreach (var order in customersWithOrders.SelectMany(d => d.Orders!))
        {
            // Reverse navigation must not enter the response graph.
            order.Customer.Should().BeNull();
        }
    }


    [Fact]
    public async Task FlexQueryAsync_TEntity_TResponse_GroupBy_ProjectsIntoTResponse()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "City",
            GroupBy = "City",
            PageSize = 100
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(parameters, opt =>
        {
            opt.MapField<CustomerDto, Customer, string>(
                dto => dto.FullName,
                entity => entity.Name);
        });

        result.Data.Should().NotBeEmpty();
        result.Data[0].City.Should().NotBeNull();
    }

    [Fact]
    public async Task FlexQueryAsync_TEntity_TResponse_GroupBy_UsingDtoMappedField_Works()
    {
        var parameters = new FlexQueryParameters
        {
            GroupBy = "CustomerName",
            PageSize = 100
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerGroupDto>(parameters, opt =>
        {
            opt.MapField<CustomerGroupDto, Customer, string>(
                dto => dto.CustomerName,
                entity => entity.Name);
        });

        result.Data.Should().NotBeEmpty();
        result.Data[0].CustomerName.Should().NotBeNull();
    }

    [Fact]
    public async Task FlexQueryAsync_TEntity_TResponse_AggregateCount_GoesToAggregatesMetadata()
    {
        // Ungrouped aggregates are result metadata on QueryResult.Aggregates — the row DTO
        // must not require generated aggregate alias properties (e.g. TotalCount).
        var parameters = new FlexQueryParameters
        {
            Aggregate = "count:Id:totalCount"
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerStatsDto>(parameters);

        result.Aggregates.Should().NotBeNull();
        Convert.ToDecimal(result.Aggregates!["Id"]["count"]).Should().BeGreaterThan(0);
        result.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FlexQueryAsync_TEntity_TResponse_AggregateSum_GoesToAggregatesMetadata()
    {
        var parameters = new FlexQueryParameters
        {
            Aggregate = "sum:Salary:totalSalary"
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerStatsDto>(parameters);

        result.Aggregates.Should().NotBeNull();
        Convert.ToDecimal(result.Aggregates!["Salary"]["sum"]).Should().BeGreaterThan(0);
        result.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FlexQueryAsync_TEntity_TResponse_MultipleAggregates_GoToAggregatesMetadata()
    {
        var parameters = new FlexQueryParameters
        {
            Aggregate = "count:Id:totalCount,sum:Salary:totalSalary"
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerStatsDto>(parameters);

        result.Aggregates.Should().NotBeNull();
        Convert.ToDecimal(result.Aggregates!["Id"]["count"]).Should().BeGreaterThan(0);
        Convert.ToDecimal(result.Aggregates["Salary"]["sum"]).Should().BeGreaterThan(0);
        result.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FlexQueryAsync_TEntity_TResponse_GroupByWithAggregates_ProjectsIntoTResponse()
    {
        var parameters = new FlexQueryParameters
        {
            GroupBy = "City",
            Aggregate = "count:Id:totalCustomers"
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CitySummaryDto>(parameters);

        result.Data.Should().NotBeEmpty();
        result.Data[0].City.Should().NotBeNull();
        result.Data[0].TotalCustomers.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FlexQueryAsync_TEntity_TResponse_GroupBy_MissingGroupKeyProperty_Throws()
    {
        var parameters = new FlexQueryParameters
        {
            GroupBy = "City"
        };

        // 'City' is not part of the public surface for CustomerStatsDto, so the
        // public-surface validation rejects the group field.
        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Customers.FlexQueryAsync<Customer, CustomerStatsDto>(parameters));

        ex.Message.Should().Contain("City");
        ex.Message.Should().Contain("CustomerStatsDto");
    }

    [Fact]
    public async Task FlexQueryAsync_TEntity_TResponse_Aggregate_MissingAliasProperty_DoesNotRequireDtoProperty()
    {
        // Salary is on the public surface; the aggregate alias 'totalSalary' is result
        // metadata on QueryResult.Aggregates — the row DTO must not need it as a property.
        var parameters = new FlexQueryParameters
        {
            Aggregate = "sum:Salary:totalSalary"
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, SalaryOnlyDto>(parameters);

        result.Aggregates.Should().NotBeNull();
        Convert.ToDecimal(result.Aggregates!["Salary"]["sum"]).Should().BeGreaterThan(0);
        result.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FlexQueryAsync_TEntity_TResponse_SameNameConvention_Works()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "city"
        };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(parameters);

        result.Data.Should().NotBeEmpty();
        result.Data.All(d => d.City != null).Should().BeTrue();
    }

    [Fact]
    public async Task FlexQueryAsync_TEntity_TResponse_DefaultProjection_OnlyResolvableFields()
    {
        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(new FlexQueryParameters(), opt =>
        {
            opt.MapField<CustomerDto, Customer, string>(
                dto => dto.FullName,
                entity => entity.Name);
        });

        result.Data.Should().NotBeEmpty();
        var first = result.Data[0];
        first.FullName.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FlexQueryAsync_TEntity_TResponse_UnmappedField_Throws()
    {
        var parameters = new FlexQueryParameters
        {
            Select = "nonexistentField"
        };

        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(parameters, opt =>
            {
                opt.MapField<CustomerDto, Customer, string>(
                    dto => dto.FullName,
                    entity => entity.Name);
            }));

        ex.Message.Should().Contain("nonexistentField");
    }

    [Fact]
    public async Task FlexQueryAsync_TEntity_TResponse_EntityOnlyField_Throws()
    {
        // 'name' exists on the entity but is not part of the public DTO surface:
        // it must be rejected in DTO-aware mode.
        var parameters = new FlexQueryParameters
        {
            Select = "name"
        };

        var ex = await Assert.ThrowsAnyAsync<FlexQueryException>(async () =>
            await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(parameters));

        ex.Message.Should().Contain("name");
    }

    [Fact]
    public async Task FlexQueryAsync_TEntity_TResponse_WithQueryOptions_AcceptsPreParsedOptions()
    {
        FlexQuery.NET.Parsers.Fql.Fql.Register();

        var parameters = new FlexQueryParameters
        {
            Filter = "fullName = \"Alice Johnson\""
        };

        var queryOptions = parameters.ToQueryOptions(QuerySyntax.Fql);

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerNameDto>(queryOptions, opt =>
        {
            opt.MapField<CustomerNameDto, Customer, string>(
                dto => dto.FullName,
                entity => entity.Name);
        });

        result.Data.Should().HaveCount(1);
        result.Data[0].FullName.Should().Be("Alice Johnson");
    }

    [Fact]
    public async Task FlexQueryAsync_TEntity_TResponse_WithQueryOptionsAndEfCoreOptions_AcceptsBoth()
    {
        FlexQuery.NET.Parsers.Fql.Fql.Register();

        var parameters = new FlexQueryParameters
        {
            Filter = "id:eq:1",
            PageSize = 1
        };

        var queryOptions = parameters.ToQueryOptions(QuerySyntax.NativeDsl);
        var efOptions = new EfCoreQueryOptions { IncludeTotalCount = true };

        var result = await _db.Customers.FlexQueryAsync<Customer, CustomerDto>(queryOptions, efOptions);

        result.Data.Should().ContainSingle();
        result.PageSize.Should().Be(1);
    }
}



