using System.Reflection;
using Microsoft.EntityFrameworkCore;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Options;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using DapperModelBuilder = FlexQuery.NET.Dapper.Configuration.ModelBuilder;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Options;

namespace FlexQuery.NET.Tests.Dapper.Security;

public class SecurityGovernanceDapperIntegrationTests
{

    private static readonly ISqlDialect Dialect = new SqliteDialect();

    private static QueryOptions NoPaging(QueryOptions options)
    {
        options.Paging.Disabled = true;
        return options;
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 11: Default projection (AllowedFields) → SELECT Id, Name
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultProjection_ShouldSelectOnlyAllowedFields()
    {
        var options = NoPaging(new QueryOptions());
        options.Items[ContextKeys.EntityType] = typeof(GovEntity);

        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" }
        };

        options.Validate(typeof(GovEntity), execOptions);

        var translator = new SqlTranslator(CreateValidationRegistry(), Dialect);
        var command = translator.Translate(options);

        command.Sql.Should().Contain("SELECT");
        command.Sql.Should().Contain("\"Id\"");
        command.Sql.Should().Contain("\"Name\"");
        command.Sql.Should().NotContain("\"SSN\"");
        command.Sql.Should().NotContain("\"Salary\"");
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 12: Blocked field exclusion
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void BlockedFields_ShouldNotAppearInSelect()
    {
        var options = NoPaging(new QueryOptions());
        options.Items[ContextKeys.EntityType] = typeof(GovEntity);

        var execOptions = new QueryExecutionOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" }
        };

        options.Validate(typeof(GovEntity), execOptions);

        var translator = new SqlTranslator(CreateValidationRegistry(), Dialect);
        var command = translator.Translate(options);

        // Positive: allowed scalar fields appear
        command.Sql.Should().Contain("\"Id\"");
        command.Sql.Should().Contain("\"Name\"");
        command.Sql.Should().Contain("\"Salary\"");
        // Negative: blocked field excluded
        command.Sql.Should().NotContain("\"SSN\"");
        // Safety: SELECT clause is not empty/collapsed
        var selectEnd = command.Sql.IndexOf("FROM", StringComparison.OrdinalIgnoreCase);
        var selectPart = command.Sql[..selectEnd];
        selectPart.Should().NotContain("SELECT ,");
        selectPart.Should().NotContain("SELECT  ");
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 13: Aggregate alias sorting
    // ──────────────────────────────────────────────────────────────

    // ──────────────────────────────────────────────────────────────
    //  Test 13: Grouped query — ORDER BY uses group field (not alias)
    //  NOTE: SqlTranslator.OrderBy does NOT remap to aggregate aliases.
    //  Aggregate alias sorting is handled in adapter layers (e.g. AG Grid).
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void GroupedQuery_OrderByGroupField_Works()
    {
        var options = NoPaging(new QueryOptions
        {
            GroupBy = new List<string> { "Category" },
            Aggregates = { new Aggregate { Field = "Price", Function = AggregateFunction.Avg, Alias = "priceAvg" } },
            Sort = { new SortNode { Field = "Category", Descending = true } }
        });
        options.Items[ContextKeys.EntityType] = typeof(GovOrder);

        var execOptions = new QueryExecutionOptions
        {
            GroupableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Category" },
            AggregatableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Price" }
        };

        options.Validate(typeof(GovOrder), execOptions);

        var translator = new SqlTranslator(CreateValidationRegistry(), Dialect);
        var command = translator.Translate(options);

        command.Sql.Should().Contain("ORDER BY");
        command.Sql.Should().Contain("\"Category\"");
        command.Sql.Should().Contain("AVG(\"Price\")");
        command.Sql.Should().Contain("priceAvg");
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 14: Grouped query – non-grouped sort field passes through
    //  NOTE: FlexQuery Core/Dapper does NOT inject fallback sorts
    //  for grouped queries. Sort-field validation against GROUP BY
    //  membership is the responsibility of adapter layers (e.g. AG Grid).
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void GroupedQuery_NonGroupedSort_ThrowsValidationError()
    {
        var options = NoPaging(new QueryOptions
        {
            GroupBy = new List<string> { "Category" },
            Aggregates = { new Aggregate { Field = "Price", Function = AggregateFunction.Sum, Alias = "priceSum" } },
            Sort = { new SortNode { Field = "Id" } }
        });
        options.Items[ContextKeys.EntityType] = typeof(GovOrder);

        var translator = new SqlTranslator(CreateValidationRegistry(), Dialect);

        Action act = () => translator.Translate(options);

        act.Should().Throw<QueryValidationException>()
            .Which.Result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.GroupBySortInvalid);
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 15: GroupBy governance — validation failure
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void GroupBy_ShouldThrow_WhenFieldNotGroupable()
    {
        var options = NoPaging(new QueryOptions
        {
            GroupBy = new List<string> { "Salary" },
            Aggregates = { new Aggregate { Field = "Salary", Function = AggregateFunction.Sum, Alias = "salarySum" } }
        });
        options.Items[ContextKeys.EntityType] = typeof(GovEntity);

        var execOptions = new QueryExecutionOptions
        {
            GroupableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name" }
        };

        var act = () => options.ValidateOrThrow<GovEntity>(execOptions);

        act.Should().Throw<QueryValidationException>();
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 16: Aggregate governance — validation failure
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Aggregate_ShouldThrow_WhenFieldNotAggregatable()
    {
        var options = NoPaging(new QueryOptions
        {
            Aggregates = { new Aggregate { Field = "Salary", Function = AggregateFunction.Sum, Alias = "salarySum" } }
        });
        options.Items[ContextKeys.EntityType] = typeof(GovEntity);

        var execOptions = new QueryExecutionOptions
        {
            AggregatableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" }
        };

        var act = () => options.ValidateOrThrow<GovEntity>(execOptions);

        act.Should().Throw<QueryValidationException>();
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 17: Having governance — validation failure
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Having_ShouldThrow_WhenFieldNotAggregatable()
    {
        var options = NoPaging(new QueryOptions
        {
            GroupBy = new List<string> { "Name" },
            Aggregates = { new Aggregate { Field = "Salary", Function = AggregateFunction.Sum, Alias = "salarySum" } },
            Having = new HavingConditionNode { Field = "Salary", Function = AggregateFunction.Sum, Operator = "gt", Value = "0" }
        });
        options.Items[ContextKeys.EntityType] = typeof(GovEntity);

        var execOptions = new QueryExecutionOptions
        {
            AggregatableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" }
        };

        var act = () => options.ValidateOrThrow<GovEntity>(execOptions);

        act.Should().Throw<QueryValidationException>();
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 18: Filter governance — FilterableFields enforcement
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Filter_ShouldThrow_WhenFieldNotInFilterableFields()
    {
        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = { new FilterCondition { Field = "SSN", Operator = "eq", Value = "123-45-6789" } }
            }
        });
        options.Items[ContextKeys.EntityType] = typeof(GovEntity);

        var execOptions = new QueryExecutionOptions
        {
            FilterableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name" }
        };

        var act = () => options.ValidateOrThrow<GovEntity>(execOptions);

        act.Should().Throw<QueryValidationException>();
    }

    [Fact]
    public void Filter_BlockedField_RemovedInNonStrictMode()
    {
        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = { new FilterCondition { Field = "SSN", Operator = "eq", Value = "123-45-6789" } }
            }
        });
        options.Items[ContextKeys.EntityType] = typeof(GovEntity);

        var execOptions = new QueryExecutionOptions
        {
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" },
            StrictFieldValidation = false
        };

        options.Validate(typeof(GovEntity), execOptions);

        // In non-strict mode the condition should be removed, leaving an empty Filters list
        options.Filter.Should().NotBeNull();
        options.Filter!.Filters.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 19: DefaultSortField — ORDER BY in SQL
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultSortField_ShouldInjectOrderBy()
    {
        var options = NoPaging(new QueryOptions());
        options.Items[ContextKeys.EntityType] = typeof(GovEntity);

        var execOptions = new QueryExecutionOptions
        {
            DefaultSortField = "Name",
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" }
        };

        options.Validate(typeof(GovEntity), execOptions);

        var translator = new SqlTranslator(CreateValidationRegistry(), Dialect);
        var command = translator.Translate(options);

        command.Sql.Should().Contain("ORDER BY");
        command.Sql.Should().Contain("\"Name\"");
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 19: ApplyPaging fallback sort — governance compliant
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void PagingFallbackSort_ShouldUseDefaultSortField()
    {
        var options = new QueryOptions
        {
            Paging = { Page = 2, PageSize = 2 }
        };
        options.Items[ContextKeys.EntityType] = typeof(GovEntity);

        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            DefaultSortField = "Name"
        };

        options.Validate(typeof(GovEntity), execOptions);

        var translator = new SqlTranslator(CreateValidationRegistry(), Dialect);
        var command = translator.Translate(options);

        command.Sql.Should().Contain("ORDER BY");
        command.Sql.Should().Contain("\"Name\"");
    }

    [Fact]
    public void PagingFallbackSort_NoDefaultSort_UsesIdAsDefaultOrderBy()
    {
        var options = new QueryOptions
        {
            Select = new List<SelectNode> { new SelectNode { Field = "Name" }, new SelectNode { Field = "Id" } },
            Paging = { Page = 2, PageSize = 2 }
        };
        options.Items[ContextKeys.EntityType] = typeof(GovEntity);

        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" }
        };

        options.Validate(typeof(GovEntity), execOptions);

        var translator = new SqlTranslator(CreateValidationRegistry(), Dialect);
        var command = translator.Translate(options);

        command.Sql.Should().Contain("ORDER BY");
        command.Sql.Should().Contain("\"Id\"");
        command.Sql.Should().Contain("LIMIT");
    }

    [Fact]
    public void PagingFallbackSort_BlockedDefaultSort_ShouldNotUseBlockedField()
    {
        var options = new QueryOptions
        {
            Paging = { Page = 2, PageSize = 2 },
            Select = new List<SelectNode> { new SelectNode { Field = "SSN" } }
        };
        options.Items[ContextKeys.EntityType] = typeof(GovEntity);

        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SSN" },
            StrictFieldValidation = false
        };

        options.Validate(typeof(GovEntity), execOptions);

        var translator = new SqlTranslator(CreateValidationRegistry(), Dialect);
        var command = translator.Translate(options);

        command.Sql.Should().NotContain("\"SSN\"");
    }

    // ──────────────────────────────────────────────────────────────
    //  Test 20: RoleAllowedFields projection
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void RoleAllowedFields_ShouldRestrictProjection()
    {
        var options = NoPaging(new QueryOptions());
        options.Items[ContextKeys.EntityType] = typeof(GovEntity);

        var execOptions = new QueryExecutionOptions
        {
            CurrentRole = "admin",
            RoleAllowedFields = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new(StringComparer.OrdinalIgnoreCase) { "Id", "Name" }
            }
        };

        options.Validate(typeof(GovEntity), execOptions);

        var translator = new SqlTranslator(CreateValidationRegistry(), Dialect);
        var command = translator.Translate(options);

        command.Sql.Should().Contain("\"Id\"");
        command.Sql.Should().Contain("\"Name\"");
        command.Sql.Should().NotContain("\"SSN\"");
        command.Sql.Should().NotContain("\"Salary\"");
    }

    // ──────────────────────────────────────────────────────────────
    //  Execution tests via Dapper API (SQLite)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_DefaultProjection_WithAllowedFields()
    {
        using var db = SqlProjectionDbContext.CreateSeeded();
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        var options = NoPaging(new QueryOptions { IncludeCount = true });
        var dapperOptions = new DapperQueryOptions
        {
            IncludeTotalCount = true,
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" }
        };
        CreateOrderRegistry(dapperOptions);
        var result = await connection.FlexQueryAsync<Customer>(options, dapperOptions);

        result.Data.Should().NotBeEmpty();
        foreach (var row in result.Data)
        {
            var dict = AssertDictionary(row);
            dict.Keys.Should().Contain("Id");
            dict.Keys.Should().Contain("Name");
            dict.Keys.Should().NotContain("Email");
            // Note: Customer has Id, Name, Email, Orders — only Id/Name are allowed
            dict.Keys.Count.Should().Be(2);
        }
    }

    [Fact]
    public void Validate_BlockedField_RemovedInNonStrictMode()
    {
        var options = NoPaging(new QueryOptions());
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var execOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" },
            BlockedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" },
            StrictFieldValidation = false
        };

        options.Validate(typeof(Customer), execOptions);

        options.Select.Should().ContainEquivalentOf(new SelectNode { Field = "Name" });
        options.Select.Should().NotContainEquivalentOf(new SelectNode { Field = "Id" });
    }

    [Fact]
    public async Task Execute_GroupedQuery_ReturnsAggregateAlias()
    {
        using var db = SqlProjectionDbContext.CreateSeeded();
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        var options = NoPaging(new QueryOptions
        {
            GroupBy = new List<string> { "CustomerId" },
            Aggregates = { new Aggregate { Field = "Total", Function = AggregateFunction.Sum, Alias = "totalSum" } },
            Sort = { new SortNode { Field = "Total", Descending = true } }
        });
        var dapperOptions = new DapperQueryOptions
        {
            IncludeTotalCount = true,
            GroupableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CustomerId" },
            AggregatableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Total" }
        };
        CreateOrderRegistry(dapperOptions);
        var result = await connection.FlexQueryAsync<Order>(options, dapperOptions);

        result.Data.Should().NotBeEmpty();
        var first = AssertDictionary(result.Data[0]);
        first.Keys.Should().Contain("totalSum");
    }

    // ──────────────────────────────────────────────────────────────
    //  Execution tests — validation failures through FlexQueryAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_GroupByGovernanceViolation_ShouldThrow()
    {
        using var db = SqlProjectionDbContext.CreateSeeded();
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        var options = NoPaging(new QueryOptions
        {
            GroupBy = new List<string> { "Name" },
            Aggregates = { new Aggregate { Field = "Name", Function = AggregateFunction.Count, Alias = "cnt" } }
        });
        var modelBuilder = new DapperModelBuilder();
        modelBuilder.Entity<GovEntity>().ToTable("Entities");
        var model = modelBuilder.Build();

        var dapperOptions = new DapperQueryOptions
        {
            IncludeTotalCount = true,
            GroupableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" }
        };
        dapperOptions.UseModel(model);
        var act = async () => await connection.FlexQueryAsync<GovEntity>(options, dapperOptions);

        await act.Should().ThrowAsync<QueryValidationException>();
    }

    [Fact]
    public async Task Execute_AggregateGovernanceViolation_ShouldThrow()
    {
        using var db = SqlProjectionDbContext.CreateSeeded();
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        var options = NoPaging(new QueryOptions
        {
            Aggregates = { new Aggregate { Field = "Name", Function = AggregateFunction.Count, Alias = "cnt" } }
        });
        var dapperOptions = new DapperQueryOptions
        {
            IncludeTotalCount = true,
            AggregatableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" }
        };
        CreateOrderRegistry(dapperOptions);
        var act = async () => await connection.FlexQueryAsync<Customer>(options, dapperOptions);

        await act.Should().ThrowAsync<QueryValidationException>();
    }

    [Fact]
    public async Task Execute_HavingGovernanceViolation_ShouldThrow()
    {
        using var db = SqlProjectionDbContext.CreateSeeded();
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        var options = NoPaging(new QueryOptions
        {
            GroupBy = new List<string> { "Name" },
            Aggregates = { new Aggregate { Field = "Name", Function = AggregateFunction.Count, Alias = "cnt" } },
            Having = new HavingConditionNode { Field = "Name", Function = AggregateFunction.Count, Operator = "gt", Value = "0" }
        });
        var dapperOptions = new DapperQueryOptions
        {
            IncludeTotalCount = true,
            AggregatableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" }
        };
        CreateOrderRegistry(dapperOptions);
        var act = async () => await connection.FlexQueryAsync<Customer>(options, dapperOptions);

        await act.Should().ThrowAsync<QueryValidationException>();
    }

    [Fact]
    public async Task Execute_FilterGovernanceViolation_ShouldThrow()
    {
        using var db = SqlProjectionDbContext.CreateSeeded();
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = { new FilterCondition { Field = "Name", Operator = "eq", Value = "Alice" } }
            }
        });
        var dapperOptions = new DapperQueryOptions
        {
            IncludeTotalCount = true,
            FilterableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" }
        };
        CreateOrderRegistry(dapperOptions);
        var act = async () => await connection.FlexQueryAsync<Customer>(options, dapperOptions);

        await act.Should().ThrowAsync<QueryValidationException>();
    }

    // ──────────────────────────────────────────────────────────────
    //  Execution test — RoleAllowedFields result verification
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_RoleAllowedFields_ShouldRestrictProjection()
    {
        using var db = SqlProjectionDbContext.CreateSeeded();
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        var options = NoPaging(new QueryOptions { IncludeCount = true });
        var dapperOptions = new DapperQueryOptions
        {
            IncludeTotalCount = true,
            CurrentRole = "admin",
            RoleAllowedFields = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new(StringComparer.OrdinalIgnoreCase) { "Id", "Name" }
            }
        };
        CreateOrderRegistry(dapperOptions);
        var result = await connection.FlexQueryAsync<Customer>(options, dapperOptions);

        result.Data.Should().NotBeEmpty();
        foreach (var row in result.Data)
        {
            var dict = AssertDictionary(row);
            dict.Keys.Should().Contain("Id");
            dict.Keys.Should().Contain("Name");
            dict.Keys.Should().NotContain("Email");
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  Execution test — DefaultSortField result ordering
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_DefaultSortField_ShouldOrderResults()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Filename=:memory:");
        await conn.OpenAsync();

        // Create and seed a table with unsorted names
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE SortTest (Id INTEGER, Name TEXT)";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "INSERT INTO SortTest VALUES (1, 'Charlie'), (2, 'Alice'), (3, 'Bob')";
        cmd.ExecuteNonQuery();

        var options = NoPaging(new QueryOptions { IncludeCount = true });
        var modelBuilder = new DapperModelBuilder();
        modelBuilder.Entity<SortTestRow>().ToTable("SortTest");
        var model = modelBuilder.Build();

        var dapperOptions = new DapperQueryOptions
        {
            IncludeTotalCount = true,
            DefaultSortField = "Name",
            AllowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" }
        };
        dapperOptions.UseModel(model);
        var result = await conn.FlexQueryAsync<SortTestRow>(options, dapperOptions);

        result.Data.Should().HaveCount(3);
        var names = result.Data
            .Select(row => (string)AssertDictionary(row)["Name"]!)
            .ToList();
        names.Should().BeInAscendingOrder();
        names[0].Should().Be("Alice");
        names[1].Should().Be("Bob");
        names[2].Should().Be("Charlie");
    }

    // ──────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────

    private static Dictionary<string, object?> AssertDictionary(object row)
    {
        if (row is Dictionary<string, object?> dict)
            return dict;

        var props = row.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in props)
            result[prop.Name] = prop.GetValue(row);
        return result;
    }

    private static IMappingRegistry CreateValidationRegistry()
    {
        var model = SharedFlexQueryModel.Instance;

        return model.Registry;
    }

    private static void CreateOrderRegistry(DapperQueryOptions options)
    {
        var builder = new DapperModelBuilder();
        builder.Entity<Customer>()
            .ToTable("Customers")
            .HasMany(c => c.Orders).HasForeignKey("CustomerId");
        builder.Entity<Order>()
            .ToTable("Orders")
            .HasMany(o => o.OrderItems).HasForeignKey("OrderId");
        builder.Entity<OrderItem>().ToTable("OrderItems");
        options.UseModel(builder.Build());
    }

    // ── Inline entity types for SQL translation tests ──────────────

    private sealed class GovEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SSN { get; set; } = string.Empty;
        public decimal Salary { get; set; }
        public string Category { get; set; } = string.Empty;
        public List<GovOrder> Orders { get; set; } = [];
    }

    private sealed class GovOrder
    {
        public int Id { get; set; }
        public int EntityId { get; set; }
        public decimal Price { get; set; }
        public string Category { get; set; } = string.Empty;
    }

    private sealed class SortTestRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
