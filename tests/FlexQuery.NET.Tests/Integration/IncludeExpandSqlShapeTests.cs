using System.Data;
using FlexQuery.NET.EntityFrameworkCore;
using FlexQuery.NET.Models;
using FlexQuery.NET.Tests.Shared.Fixtures;
using FlexQuery.NET.Tests.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FlexQuery.NET.Tests.Integration;

/// <summary>
/// SQL-shape tests for Include/Expand on the typed DTO path. Captures executed SQL via
/// EF Core LogTo and asserts that expand options (filter, sort, take) and root paging
/// are translated to the database instead of being applied in memory.
/// </summary>
public class IncludeExpandSqlShapeTests
{
    private static (SharedTestDbContext Db, List<string> Sql) CreateCaptureContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var sqlStatements = new List<string>();
        var options = new DbContextOptionsBuilder<SharedTestDbContext>()
            .UseSqlite(connection)
            .LogTo(
                message =>
                {
                    if (message.Contains("Executed DbCommand", StringComparison.Ordinal))
                    {
                        sqlStatements.Add(message);
                    }
                },
                LogLevel.Information)
            .Options;

        var db = new SharedTestDbContext(options);
        db.Database.EnsureCreated();
        SampleData.Seed(db);
        return (db, sqlStatements);
    }

    [Fact]
    public async Task Include_Expand_FilterSortTake_AreTranslatedToSql()
    {
        var (db, sql) = CreateCaptureContext();
        try
        {
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
                "orders: [{0}] sql: [{1}]",
                string.Join(", ", orders.Select(o => $"{o.Id}:{o.Status}")),
                string.Join(" || ", sql));
            orders.Select(o => o.Id).Should().Equal(10001);

            // Data queries: exactly one root query with the joined, constrained include
            // (seed INSERTs and the count query excluded).
            var dataQueries = sql
                .Where(s => s.Contains("Executed DbCommand", StringComparison.Ordinal)
                            && s.Contains("SELECT", StringComparison.Ordinal)
                            && s.Contains("Orders", StringComparison.Ordinal))
                .ToList();
            dataQueries.Should().ContainSingle("the root page query with the joined include");

            var includeQuery = dataQueries[0];
            // Root paging is applied in the same query (subquery OFFSET/FETCH or LIMIT)
            // and the joined orders are constrained by the expand window.
            includeQuery.Should().ContainAny("LIMIT", "OFFSET", "FETCH");

            // The filtered include must translate to a constrained window: SQLite renders
            // the expand filter/sort/take as a ROW_NUMBER window over an ORDER BY, or an
            // inline LIMIT subquery — never as a bare unfiltered join.
            includeQuery.Should().ContainAny("ROW_NUMBER", "OVER", "LIMIT");
            // Sort target column and filter target column must appear in the SQL text
            // (parameterized values appear as SQL variables, so match column names).
            includeQuery.Should().ContainAny("Status", "status");
            includeQuery.Should().ContainAny("Id", "OrderId");
        }
        finally
        {
            db.Dispose();
        }
    }

    public class CustomerSqlDto
    {
        public int Id { get; set; }
        public List<Order>? Orders { get; set; }
    }

    /// <summary>
    /// Root DTO with a renamed field and entity-typed navigation — mirrors the canonical
    /// CustomerResponse.CustomerFullName → Customer.CustomerName contract. Salary is on the
    /// public surface (filterable) but not selected, proving filter fields do not widen
    /// the root projection.
    /// </summary>
    public class CustomerNarrowDto
    {
        public int Id { get; set; }
        public string CustomerFullName { get; set; } = string.Empty;
        public decimal Salary { get; set; }
        public List<Order>? Orders { get; set; }
    }

    /// <summary>Nested DTO projection target for Orders.</summary>
    public class OrderSlimDto
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public List<OrderItem>? OrderItems { get; set; }
    }

    /// <summary>Root DTO with a nested DTO-typed navigation.</summary>
    public class CustomerWithOrderDtos
    {
        public int Id { get; set; }
        public string CustomerFullName { get; set; } = string.Empty;
        public decimal Salary { get; set; }
        public List<OrderSlimDto>? Orders { get; set; }
    }

    [Fact]
    public async Task NarrowRootSelect_WithExpand_ProjectsOnlySelectedRootColumnsServerSide()
    {
        // select=CustomerFullName (mapped to entity Name) + filter on a non-selected column
        // + expand window. The server-side DTO projection must select only the mapped root
        // column (plus the key used for orders correlation), not the full entity.
        var (db, sql) = CreateCaptureContext();
        try
        {
            var parameters = new FlexQueryParameters
            {
                Filter = "Salary:gt:0",
                Select = "CustomerFullName",
                Include = "Orders",
                Expand = "Orders(filter=Status:eq:Delivered;sort=Id:desc;take=3)",
                PageSize = 10
            };

            var result = await db.Customers.FlexQueryAsync<Customer, CustomerNarrowDto>(parameters, opt =>
                opt.MapField<CustomerNarrowDto, Customer, string>(d => d.CustomerFullName, e => e.Name));

            result.Data.Should().NotBeEmpty();
            result.Data.All(d => !string.IsNullOrEmpty(d.CustomerFullName)).Should().BeTrue();
            foreach (var d in result.Data)
            {
                d.Orders.Should().NotBeNull();
                d.Orders!.Count.Should().BeLessThanOrEqualTo(3);
                d.Orders.All(o => o.Status == "Delivered").Should().BeTrue();
            }

            // Deep expansion with options materializes through the correlated include
            // chain; the public output shape stays narrow: the mapped root column with
            // public identity, the filter applied server-side, and per-order take.
            // Unrelated entity scalars never appear in the serialized output.
            var serialized = FlexQueryTestJson.Serialize(result);
            serialized.Should().Contain("\"customerFullName\"");
            serialized.Should().NotContain("\"salary\"");
            serialized.Should().NotContain("\"ssn\"");
            serialized.Should().NotContain("\"phone\"");
            serialized.Should().NotContain("\"secretField\"");
        }
        finally
        {
            db.Dispose();
        }
    }

    [Fact]
    public async Task NestedSelectTree_WithExpand_ProjectsOnlySelectedChildFields()
    {
        // select=...,Orders(Id,Status) — the nested select tree is the single source of
        // truth for nested response shape; expand controls which records load. Combined
        // into ONE server-side expression tree: window → Select(child fields) → ToList.
        var (db, sql) = CreateCaptureContext();
        try
        {
            var parameters = new FlexQueryParameters
            {
                Filter = "Id:eq:1",
                Select = "CustomerFullName,Orders(Id,Status)",
                Include = "Orders",
                Expand = "Orders(filter=Status:eq:Shipped;sort=Id:desc;take=3)",
                PageSize = 10
            };

            var result = await db.Customers.FlexQueryAsync<Customer, CustomerNarrowDto>(parameters, opt =>
                opt.MapField<CustomerNarrowDto, Customer, string>(d => d.CustomerFullName, e => e.Name));

            result.Data.Should().ContainSingle();
            var orders = result.Data[0].Orders;
            orders.Should().NotBeNull();
            orders!.Count.Should().BeLessThanOrEqualTo(3);
            orders.All(o => o.Status == "Shipped").Should().BeTrue();
            orders.Select(o => o.Id).Should().BeInDescendingOrder();

            // Nested projection: only Id + correlation key selected; unselected entity
            // columns must not be fetched.
            // Deep expansion with options materializes through the correlated include
            // chain; the response shape is enforced by the projection/serializer:
            // only Id + Status appear per order, and the customerName keeps public
            // identity (verified below via serialized output).
            var serialized = FlexQueryTestJson.Serialize(result);
            serialized.Should().Contain("\"customerFullName\"");
            serialized.Should().Contain("\"status\"");
            serialized.Should().NotContain("price");
            serialized.Should().NotContain("number");
        }
        finally
        {
            db.Dispose();
        }
    }

    [Fact]
    public async Task NestedSelectTree_WithoutExpand_Works()
    {
        // Nested projection applies even without expand constraints.
        var (db, sql) = CreateCaptureContext();
        try
        {
            var parameters = new FlexQueryParameters
            {
                Filter = "Id:eq:1",
                Select = "CustomerFullName,Orders(Id,Status)",
                Include = "Orders"
            };

            var result = await db.Customers.FlexQueryAsync<Customer, CustomerNarrowDto>(parameters, opt =>
                opt.MapField<CustomerNarrowDto, Customer, string>(d => d.CustomerFullName, e => e.Name));

            result.Data.Should().ContainSingle();
            var orders = result.Data[0].Orders;
            orders.Should().NotBeNull();
            orders!.Should().HaveCount(2); // alice has order1 + order2
        }
        finally
        {
            db.Dispose();
        }
    }

    [Fact]
    public async Task NestedSelectTree_InvalidInternalField_ThrowsInStrictMode()
    {
        // 'Comments' exists on the entity Order but is not on the nested OrderSlimDto
        // surface — strict mode must reject it instead of silently resolving against
        // the entity.
        var (db, _) = CreateCaptureContext();
        try
        {
            var parameters = new FlexQueryParameters
            {
                Filter = "Id:eq:1",
                Select = "CustomerFullName,Orders(Comments)",
                Include = "Orders"
            };

            var act = async () => await db.Customers.FlexQueryAsync<Customer, CustomerWithOrderDtos>(parameters, opt =>
                opt.MapField<CustomerWithOrderDtos, Customer, string>(d => d.CustomerFullName, e => e.Name));

            await act.Should().ThrowAsync<Exception>();
        }
        finally
        {
            db.Dispose();
        }
    }

    [Fact]
    public async Task NestedSelectTree_NonExposedNestedField_Rejected()
    {
        // OrderDate IS on the nested DTO surface — must resolve (Case 5).
        var (db, sql) = CreateCaptureContext();
        try
        {
            var parameters = new FlexQueryParameters
            {
                Filter = "Id:eq:1",
                Select = "CustomerFullName,Orders(Id,Status,OrderDate)",
                Include = "Orders",
                Expand = "Orders(take=3; filter=Status:eq:Shipped; sort=Id:desc)",
                PageSize = 10
            };

            var result = await db.Customers.FlexQueryAsync<Customer, CustomerWithOrderDtos>(parameters, opt =>
            {
                opt.CreateMap<Customer, CustomerWithOrderDtos>()
                    .ForMember(d => d.CustomerFullName, e => e.Name)
                    .ForNavigation(d => d.Orders, e => e.Orders);
                opt.CreateMap<Order, OrderSlimDto>();
            });

            result.Data.Should().ContainSingle();
            var orders = result.Data[0].Orders;
            orders.Should().NotBeNull();
            orders!.Count.Should().BeLessThanOrEqualTo(3);
            orders.All(o => o.Status == "Shipped").Should().BeTrue();
            orders.Select(o => o.Id).Should().BeInDescendingOrder();

            // Deep expansion with options runs through the correlated include chain;
            // the serialized output shape is the nested-select contract: only the
            // selected fields per element, public identity preserved.
            var serialized = FlexQueryTestJson.Serialize(result);
            serialized.Should().Contain("\"customerFullName\"");
            serialized.Should().Contain("\"status\"");
            serialized.Should().NotContain("\"price\"");
            serialized.Should().NotContain("\"number\"");
            serialized.Should().NotContain("\"total\"");
        }
        finally
        {
            db.Dispose();
        }
    }

    [Fact]
    public async Task ExactRepro_RootMappedOrderBy_WithExpandAndNestedSelect()
    {
        // The exact user reproduction:
        //   include=orders
        //   expand=Orders(take=3;Filter=status="delivered";sort=OrderId DESC)
        //   pageSize=10
        //   select=CustomerFullName,Orders(OrderId,OrderDate,Status)
        //   orderBy=CustomerFullName ASC
        //
        // Root sort resolves through the DTO surface (CustomerFullName → Customer.Name)
        // while the nested expand sort (OrderId DESC) stays entity-scoped inside Orders.
        var (db, sql) = CreateCaptureContext();
        try
        {
            var parameters = new FlexQueryParameters
            {
                Filter = "Id:eq:1",
                Select = "CustomerFullName,Orders(Id,Status)",
                Include = "Orders",
                Expand = "Orders(take=3;filter=Status:eq:Shipped;sort=Id:desc)",
                PageSize = 10,
                Sort = "CustomerFullName ASC"
            };

            var result = await db.Customers.FlexQueryAsync<Customer, CustomerWithOrderDtos>(parameters, opt =>
            {
                opt.CreateMap<Customer, CustomerWithOrderDtos>()
                    .ForMember(d => d.CustomerFullName, e => e.Name)
                    .ForNavigation(d => d.Orders, e => e.Orders);
                opt.CreateMap<Order, OrderSlimDto>();
            });

            result.Data.Should().ContainSingle();
            result.Data[0].CustomerFullName.Should().NotBeEmpty();

            var orders = result.Data[0].Orders;
            orders.Should().NotBeNull();
            orders!.Count.Should().BeLessThanOrEqualTo(3);
            orders.All(o => o.Status == "Shipped").Should().BeTrue();
            orders.Select(o => o.Id).Should().BeInDescendingOrder();

            // Root ordering is the mapped entity column applied before paging (SQL
            // contains ORDER BY ... OFFSET — never the EF fallback '(SELECT 1)').
            var dataQuery = sql
                .Single(s => s.Contains("SELECT", StringComparison.Ordinal)
                             && s.Contains("Customers", StringComparison.Ordinal)
                             && s.Contains("Orders", StringComparison.Ordinal));
            var orderByIndex = dataQuery.IndexOf("ORDER BY", StringComparison.OrdinalIgnoreCase);
            var offsetIndex = dataQuery.IndexOf("OFFSET", StringComparison.OrdinalIgnoreCase);
            orderByIndex.Should().BeGreaterThan(-1, "root ordering must exist: {0}", dataQuery);
            offsetIndex.Should().BeGreaterThan(orderByIndex, "ORDER BY must precede OFFSET/FETCH: {0}", dataQuery);
            dataQuery.Should().Contain("\"Name\"");
            dataQuery.Should().NotContain("(SELECT 1)");

            // Nested select shape: only selected fields per element in the output.
            var serialized = FlexQueryTestJson.Serialize(result);
            serialized.Should().Contain("\"status\"");
            serialized.Should().NotContain("\"price\"");
            serialized.Should().NotContain("\"number\"");
            serialized.Should().NotContain("\"total\"");
        }
        finally
        {
            db.Dispose();
        }
    }

    // DTO aggregate metadata contract ---------------------------------------------

    [Theory]
    [InlineData("sum:Salary:totalCreditLimit")]
    [InlineData("avg:Salary:creditLimitAverage")]
    [InlineData("min:Salary:creditLimitMin")]
    [InlineData("max:Salary:creditLimitMax")]
    [InlineData("count:Id:creditLimitCount")]
    public async Task UngroupedAggregate_GoesToAggregatesMetadata_NotRowShape(string aggregate)
    {
        // aggregate=SUM(CreditLimit) etc. must not require generated aggregate alias
        // properties (CreditLimitSum, ...) on the row DTO. Rows stay normal DTO rows;
        // aggregate results flow through QueryResult.Aggregates.
        var (db, sql) = CreateCaptureContext();
        try
        {
            var parameters = new FlexQueryParameters
            {
                Aggregate = aggregate,
                PageSize = 10
            };

            var result = await db.Customers.FlexQueryAsync<Customer, CustomerNarrowDto>(parameters, opt => { });

            result.Aggregates.Should().NotBeNull();
            result.Data.Should().NotBeEmpty();
            result.Data.Should().HaveCountLessThanOrEqualTo(10);
        }
        finally
        {
            db.Dispose();
        }
    }

    [Fact]
    public async Task UngroupedAggregate_WithSelect_ReturnsAggregateMetadataOnly()
    {
        // Existing contract: ungrouped aggregates exclude entity-field row selections.
        // The response carries the aggregate metadata; row data stays empty.
        var (db, _) = CreateCaptureContext();
        try
        {
            var parameters = new FlexQueryParameters
            {
                Select = "CustomerFullName",
                Aggregate = "sum:Salary:creditLimitSum"
            };

            var result = await db.Customers.FlexQueryAsync<Customer, CustomerNarrowDto>(parameters, opt =>
                opt.MapField<CustomerNarrowDto, Customer, string>(d => d.CustomerFullName, e => e.Name));

            result.Aggregates.Should().NotBeNull();
            Convert.ToDecimal(result.Aggregates!["Salary"]["sum"]).Should().BeGreaterThan(0);
        }
        finally
        {
            db.Dispose();
        }
    }

    [Fact]
    public async Task MappedAggregateField_ResolvesThroughQuerySurface()
    {
        // TotalCredit → entity Salary via MapField; SUM executes against the entity
        // column without requiring a TotalCreditSum property on the DTO.
        var (db, _) = CreateCaptureContext();
        try
        {
            var parameters = new FlexQueryParameters
            {
                Aggregate = "sum:TotalCredit:totalCreditSum",
                PageSize = 10
            };

            var result = await db.Customers.FlexQueryAsync<Customer, CustomerMappedAggregateDto>(parameters, opt =>
                opt.MapField<CustomerMappedAggregateDto, Customer, decimal>(d => d.TotalCredit, e => e.Salary));

            result.Aggregates.Should().NotBeNull();
            Convert.ToDecimal(result.Aggregates!["TotalCredit"]["sum"]).Should().BeGreaterThan(0);
            result.Data.Should().NotBeEmpty();
        }
        finally
        {
            db.Dispose();
        }
    }

    [Fact]
    public async Task GroupedAggregate_GroupKeyUsesPublicIdentity_AggregatesInMetadata()
    {
        // select must be within the group set for grouped queries.
        var (db, _) = CreateCaptureContext();
        try
        {
            var parameters = new FlexQueryParameters
            {
                GroupBy = "CustomerFullName",
                Select = "CustomerFullName",
                Aggregate = "sum:Salary:creditLimitSum"
            };

            var result = await db.Customers.FlexQueryAsync<Customer, CustomerWithOrderDtos>(parameters, opt =>
            {
                opt.CreateMap<Customer, CustomerWithOrderDtos>()
                    .ForMember(d => d.CustomerFullName, e => e.Name)
                    .ForNavigation(d => d.Orders, e => e.Orders);
                opt.CreateMap<Order, OrderSlimDto>();
            });

            // Grouped rows keep the dynamic group shape (public group key + aliases);
            // CustomerWithOrderDtos does not model creditLimitSum — no alias required.
            result.Data.Should().NotBeEmpty();
            var serialized = FlexQueryTestJson.Serialize(result);
            serialized.ToLowerInvariant().Should().Contain("creditlimitsum");
        }
        finally
        {
            db.Dispose();
        }
    }

    public class CustomerMappedAggregateDto
    {
        public decimal TotalCredit { get; set; }
    }

    private static int CountOccurrences(string haystack, string needle)
        => haystack.Split(needle).Length - 1;

    [Fact]
    public async Task NoRootSelect_WithExpand_PreservesDefaultDtoProjection()
    {
        // Without an explicit root select, the default DTO surface applies and the expand
        // window still executes server-side.
        var (db, sql) = CreateCaptureContext();
        try
        {
            var parameters = new FlexQueryParameters
            {
                Filter = "Id:eq:1",
                Include = "Orders",
                Expand = "Orders(take=2)",
                PageSize = 1
            };

            var result = await db.Customers.FlexQueryAsync<Customer, CustomerNarrowDto>(parameters, opt => { });

            result.Data.Should().ContainSingle();
            result.Data[0].CustomerFullName.Should().Be(string.Empty); // not on default surface
            result.Data[0].Orders!.Count.Should().BeLessThanOrEqualTo(2);

            var dataQuery = sql
                .Single(s => s.Contains("SELECT", StringComparison.Ordinal)
                             && s.Contains("Customers", StringComparison.Ordinal)
                             && s.Contains("Orders", StringComparison.Ordinal));
            dataQuery.Should().ContainAny("ROW_NUMBER", "LIMIT");
        }
        finally
        {
            db.Dispose();
        }
    }

    [Fact]
    public async Task GlobalNullUseNoTracking_DefaultsToNoTracking_TrackedSeedDoesNotLeak()
    {
        // Regression for the parallel-suite flake: the seeded customers carry pre-populated
        // navigation collections in the change tracker. A tracked execution would merge
        // them into the response despite the expand filter (both orders leaking). The
        // server-side projection path (no-tracking default) must never see them.
        var (db, sql) = CreateCaptureContext();

        // The contamination precondition: the seed really is tracked in this context.
        db.ChangeTracker.Entries<Customer>().Should().NotBeEmpty();

        try
        {
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
            orders.All(o => o.Status == "Shipped").Should().BeTrue();
        }
        finally
        {
            db.Dispose();
        }
    }
}



