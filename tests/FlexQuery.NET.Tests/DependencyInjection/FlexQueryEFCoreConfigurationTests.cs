using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.EntityFrameworkCore.Configuration;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared;
using FlexQuery.NET.Tests.Shared.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FlexQuery.NET.Tests.DependencyInjection;

[Collection("GlobalConfiguration")]
public class FlexQueryEFCoreConfigurationTests
{
    public FlexQueryEFCoreConfigurationTests()
    {
        FlexQueryEFCore.Reset();
    }

    [Fact]
    public void Setup_Does_Not_Throw()
    {
        Action act = () => FlexQueryEFCore.Setup();

        act.Should().NotThrow();
    }

    [Fact]
    public void Configure_Stores_Global_Options()
    {
        FlexQueryEFCore.Configure(null);

        FlexQueryEFCore.DefaultOptions.Should().NotBeNull();
    }

    [Fact]
    public void Configure_Without_Delegate_Sets_Default_Options()
    {
        FlexQueryEFCore.Configure(null);

        FlexQueryEFCore.DefaultOptions.Should().NotBeNull();
    }

    [Fact]
    public async Task GlobalUseNoTrackingNull_KeepsPackageDefault_NoTrackedLeakThroughDtoProjection()
    {
        // Regression: a global FlexQueryEFCore.Configure(...) without an explicit
        // UseNoTracking must NOT downgrade the package default (no-tracking) to tracked
        // execution. With tracked execution, seed-populated navigation collections leak
        // through typed DTO projections even when the expand window filters them.
        FlexQueryEFCore.Configure(null); // global UseNoTracking = null (defect trigger)

        var db = new SharedTestDbContext(
            new DbContextOptionsBuilder<SharedTestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        db.Database.EnsureCreated();
        SampleData.Seed(db);

        try
        {
            // Contamination precondition: the seed is tracked in this context.
            db.ChangeTracker.Entries<Customer>().Should().NotBeEmpty();

            var parameters = new FlexQueryParameters
            {
                Filter = "Id:eq:1",
                Include = "Orders",
                Expand = "Orders(filter=Status:eq:Shipped;sort=Id:desc;take=2)",
                PageSize = 1
            };

            var result = await db.Customers.FlexQueryAsync<Customer, CustomerSqlDto>(parameters, opt => { });

            result.Data.Should().ContainSingle();
            var orders = result.Data[0].Orders;
            orders.Should().NotBeNull();
            orders!.Count.Should().BeLessThanOrEqualTo(2);
            orders.All(o => o.Status == "Shipped").Should().BeTrue(
                "orders: [{0}]",
                string.Join(", ", orders.Select(o => $"{o.Id}:{o.Status}")));
        }
        finally
        {
            FlexQueryEFCore.Reset();
            db.Dispose();
        }
    }

    public class CustomerSqlDto
    {
        public int Id { get; set; }
        public List<Order>? Orders { get; set; }
    }
}

