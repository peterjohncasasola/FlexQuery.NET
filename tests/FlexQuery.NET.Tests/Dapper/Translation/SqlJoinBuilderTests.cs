using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Builders;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Tests.Dapper.Translation;

public class SqlJoinBuilderTests
{
    private readonly IMappingRegistry _registry = new MappingRegistry();
    private static readonly ISqlDialect Dialect = new SqlServerDialect();

    public SqlJoinBuilderTests()
    {
        _registry.Entity<SqlCustomer>()
            .ToTable("Customers")
            .HasMany(c => c.Orders).WithForeignKey("CustomerId");
        _registry.Entity<SqlOrder>()
            .ToTable("Orders")
            .HasMany(o => o.Items).WithForeignKey("OrderId");
        _registry.Entity<SqlOrderItem>()
            .ToTable("OrderItems");
    }

    private SqlJoinBuilder CreateBuilder(ISqlDialect? dialect = null)
    {
        var d = dialect ?? Dialect;
        var includeTranslator = new SqlIncludeTranslator(d);
        var existsTranslator = new SqlExistsTranslator(d);
        var countTranslator = new SqlCountTranslator(d);
        var whereBuilder = new SqlWhereBuilder(_registry, d, existsTranslator, countTranslator);
        return new SqlJoinBuilder(_registry, d, includeTranslator, whereBuilder);
    }

    [Fact]
    public void BuildJoinClause_NoSelectTreeNoIncludes_ReturnsEmpty()
    {
        var mapping = _registry.GetMapping(typeof(SqlCustomer));
        var builder = CreateBuilder();
        var options = new QueryOptions();
        var parameters = new SqlParameterContext(Dialect);
        var tree = new SelectionNode();

        var result = builder.BuildJoinClause(options, mapping, parameters, tree);

        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildJoinClause_SelectTreeWithNavigation_GeneratesJoin()
    {
        var mapping = _registry.GetMapping(typeof(SqlCustomer));
        var builder = CreateBuilder();
        var options = new QueryOptions();
        var parameters = new SqlParameterContext(Dialect);
        var tree = new SelectionNode();
        tree.GetOrAddChild("Orders").GetOrAddChild("Total");

        var result = builder.BuildJoinClause(options, mapping, parameters, tree);

        result.Should().Contain("LEFT JOIN");
        result.Should().Contain("[Orders]");
        result.Should().Contain("[CustomerId]");
        result.Should().Contain("[Customers]");
    }

    [Fact]
    public void BuildJoinClause_MultiLevelNavigation_GeneratesMultipleJoins()
    {
        var mapping = _registry.GetMapping(typeof(SqlCustomer));
        var builder = CreateBuilder();
        var options = new QueryOptions();
        var parameters = new SqlParameterContext(Dialect);
        var tree = new SelectionNode();
        tree.GetOrAddChild("Orders").GetOrAddChild("Items").GetOrAddChild("Sku");

        var result = builder.BuildJoinClause(options, mapping, parameters, tree);

        result.Should().Contain("LEFT JOIN [Orders]");
        result.Should().Contain("LEFT JOIN [OrderItems]");
    }

    [Fact]
    public void BuildJoinClause_WithIncludes_GeneratesJoin()
    {
        var mapping = _registry.GetMapping(typeof(SqlCustomer));
        var builder = CreateBuilder();
        var options = new QueryOptions
        {
            Includes = ["Orders"]
        };
        var parameters = new SqlParameterContext(Dialect);
        var tree = new SelectionNode();

        var result = builder.BuildJoinClause(options, mapping, parameters, tree);

        result.Should().Contain("LEFT JOIN");
        result.Should().Contain("[Orders]");
    }

    [Fact]
    public void BuildJoinClause_WithFilteredInclude_GeneratesJoinWithFilter()
    {
        var mapping = _registry.GetMapping(typeof(SqlCustomer));
        var builder = CreateBuilder();
        var options = new QueryOptions
        {
            Expand =
            [
                new IncludeNode
                {
                    Path = "Orders",
                    Filter = new FilterGroup
                    {
                        Filters = [new FilterCondition { Field = "Total", Operator = "gt", Value = "100" }]
                    }
                }
            ]
        };
        var parameters = new SqlParameterContext(Dialect);
        var tree = new SelectionNode();

        var result = builder.BuildJoinClause(options, mapping, parameters, tree);

        result.Should().Contain("LEFT JOIN");
        result.Should().Contain("[Orders]");
        result.Should().Contain("[Total] > @p0");
        parameters.Parameters["@p0"].Should().Be(100M);
    }

    [Fact]
    public void BuildJoinClause_DuplicatePaths_Deduplicates()
    {
        var mapping = _registry.GetMapping(typeof(SqlCustomer));
        var builder = CreateBuilder();
        var options = new QueryOptions
        {
            Includes = ["Orders", "Orders"]
        };
        var parameters = new SqlParameterContext(Dialect);
        var tree = new SelectionNode();

        var result = builder.BuildJoinClause(options, mapping, parameters, tree);

        result.Should().Contain("LEFT JOIN [Orders]");
        var count = System.Text.RegularExpressions.Regex.Matches(result, "LEFT JOIN").Count;
        count.Should().Be(1);
    }

    [Fact]
    public void BuildJoinClause_SelectTreeAndIncludes_Deduplicates()
    {
        var mapping = _registry.GetMapping(typeof(SqlCustomer));
        var builder = CreateBuilder();
        var options = new QueryOptions
        {
            Includes = ["Orders"]
        };
        var parameters = new SqlParameterContext(Dialect);
        var tree = new SelectionNode();
        tree.GetOrAddChild("Orders").GetOrAddChild("Total");

        var result = builder.BuildJoinClause(options, mapping, parameters, tree);

        var count = System.Text.RegularExpressions.Regex.Matches(result, "LEFT JOIN").Count;
        count.Should().Be(1);
    }

    [Fact]
    public void BuildJoinClause_UnknownPath_IsSkipped()
    {
        var mapping = _registry.GetMapping(typeof(SqlCustomer));
        var builder = CreateBuilder();
        var options = new QueryOptions
        {
            Includes = ["NonExistent"]
        };
        var parameters = new SqlParameterContext(Dialect);
        var tree = new SelectionNode();

        var result = builder.BuildJoinClause(options, mapping, parameters, tree);

        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildJoinClause_WithPostgreSql_UsesCorrectQuoting()
    {
        var dialect = new PostgreSqlDialect();
        var mapping = _registry.GetMapping(typeof(SqlCustomer));
        var builder = CreateBuilder(dialect);
        var options = new QueryOptions
        {
            Includes = ["Orders"]
        };
        var parameters = new SqlParameterContext(dialect);
        var tree = new SelectionNode();

        var result = builder.BuildJoinClause(options, mapping, parameters, tree);

        result.Should().Contain("\"Orders\"");
        result.Should().Contain("\"Customers\"");
    }

    [Fact]
    public void BuildJoinClause_WithMySql_UsesCorrectQuoting()
    {
        var dialect = new MySqlDialect();
        var mapping = _registry.GetMapping(typeof(SqlCustomer));
        var builder = CreateBuilder(dialect);
        var options = new QueryOptions
        {
            Includes = ["Orders"]
        };
        var parameters = new SqlParameterContext(dialect);
        var tree = new SelectionNode();

        var result = builder.BuildJoinClause(options, mapping, parameters, tree);

        result.Should().Contain("`Orders`");
        result.Should().Contain("`Customers`");
    }

    [Fact]
    public void BuildJoinClause_CaseInsensitiveFilter_AppliesCorrectly()
    {
        var mapping = _registry.GetMapping(typeof(SqlCustomer));
        var builder = CreateBuilder();
        var options = new QueryOptions
        {
            CaseInsensitive = true,
            Expand =
            [
                new IncludeNode
                {
                    Path = "Orders",
                    Filter = new FilterGroup
                    {
                        Filters = [new FilterCondition { Field = "Number", Operator = "eq", Value = "ORD-001" }]
                    }
                }
            ]
        };
        var parameters = new SqlParameterContext(Dialect);
        var tree = new SelectionNode();

        var result = builder.BuildJoinClause(options, mapping, parameters, tree);

        result.Should().Contain("LOWER(");
        result.Should().Contain("LEFT JOIN");
    }

    [Fact]
    public void BuildJoinClause_SelectTreeWithFilteredNavigation_GeneratesFilteredJoin()
    {
        var mapping = _registry.GetMapping(typeof(SqlCustomer));
        var builder = CreateBuilder();
        var options = new QueryOptions();
        var parameters = new SqlParameterContext(Dialect);
        var tree = new SelectionNode();
        var ordersNode = tree.GetOrAddChild("Orders");
        ordersNode.GetOrAddChild("Total");
        ordersNode.Filter = new FilterGroup
        {
            Filters = [new FilterCondition { Field = "Total", Operator = "gt", Value = "50" }]
        };

        var result = builder.BuildJoinClause(options, mapping, parameters, tree);

        result.Should().Contain("LEFT JOIN");
        result.Should().Contain("[Total] > @p0");
    }

    [Fact]
    public void BuildJoinClause_WithTableAlias_UsesAliasInJoinCondition()
    {
        var registry = new MappingRegistry();
        registry.Entity<SqlCustomer>()
            .ToTable("Customers").HasAlias("c")
            .HasMany(c => c.Orders).WithForeignKey("CustomerId");
        registry.Entity<SqlOrder>()
            .ToTable("Orders");

        var mapping = registry.GetMapping(typeof(SqlCustomer));
        var includeTranslator = new SqlIncludeTranslator(Dialect);
        var existsTranslator = new SqlExistsTranslator(Dialect);
        var countTranslator = new SqlCountTranslator(Dialect);
        var whereBuilder = new SqlWhereBuilder(registry, Dialect, existsTranslator, countTranslator);
        var builder = new SqlJoinBuilder(registry, Dialect, includeTranslator, whereBuilder);
        var options = new QueryOptions
        {
            Includes = ["Orders"]
        };
        var parameters = new SqlParameterContext(Dialect);
        var tree = new SelectionNode();

        var result = builder.BuildJoinClause(options, mapping, parameters, tree);

        result.Should().Contain("[c]");
    }

    [Fact]
    public void BuildJoinClause_MultipleFilteredIncludes_GeneratesMultipleJoins()
    {
        var registry = new MappingRegistry();
        registry.Entity<SqlCustomer>()
            .ToTable("Customers")
            .HasMany(c => c.Orders).WithForeignKey("CustomerId");
        registry.Entity<SqlCustomer>()
            .HasOne(c => c.Address).WithForeignKey("CustomerId");
        registry.Entity<SqlOrder>()
            .ToTable("Orders");
        registry.Entity<SqlAddress>()
            .ToTable("Addresses");

        var mapping = registry.GetMapping(typeof(SqlCustomer));
        var includeTranslator = new SqlIncludeTranslator(Dialect);
        var existsTranslator = new SqlExistsTranslator(Dialect);
        var countTranslator = new SqlCountTranslator(Dialect);
        var whereBuilder = new SqlWhereBuilder(registry, Dialect, existsTranslator, countTranslator);
        var builder = new SqlJoinBuilder(registry, Dialect, includeTranslator, whereBuilder);
        var options = new QueryOptions
        {
            Includes = ["Orders", "Address"]
        };
        var parameters = new SqlParameterContext(Dialect);
        var tree = new SelectionNode();

        var result = builder.BuildJoinClause(options, mapping, parameters, tree);

        result.Should().Contain("[Orders]");
        result.Should().Contain("[Addresses]");
    }
}
