using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Builders;
using FlexQuery.NET.Internal;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Tests.Dapper.Translation;

public class SqlSelectBuilderTests
{
    private readonly IMappingRegistry _registry = SharedFlexQueryModel.Instance.Registry;
    private static readonly ISqlDialect Dialect = new SqlServerDialect();

    
    // ── BuildAggregateSelectParts ────────────────────────────────────────

    [Fact]
    public void BuildAggregateSelectParts_CountStarNullField_ReturnsCount1()
    {
        var mapping = _registry.GetMapping(typeof(Customer));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Count, Field = null, Alias = "total" }]
        };

        var parts = builder.BuildAggregateSelectParts(options, mapping);

        parts.Should().ContainSingle().Which.Should().Be("COUNT(1) AS [total]");
    }

    [Fact]
    public void BuildAggregateSelectParts_CountStarWildcard_ReturnsCount1()
    {
        var mapping = _registry.GetMapping(typeof(Customer));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Count, Field = "*", Alias = "total" }]
        };

        var parts = builder.BuildAggregateSelectParts(options, mapping);

        parts.Should().ContainSingle().Which.Should().Be("COUNT(1) AS [total]");
    }

    [Fact]
    public void BuildAggregateSelectParts_CountField_GeneratesCountField()
    {
        var mapping = _registry.GetMapping(typeof(Customer));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Count, Field = "Status", Alias = "statusCount" }]
        };

        var parts = builder.BuildAggregateSelectParts(options, mapping);

        parts.Should().ContainSingle().Which.Should().Be("COUNT([Status]) AS [statusCount]");
    }

    [Fact]
    public void BuildAggregateSelectParts_Sum_GeneratesSum()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Sum, Field = "Score", Alias = "totalScore" }]
        };

        var parts = builder.BuildAggregateSelectParts(options, mapping);

        parts.Should().ContainSingle().Which.Should().Be("SUM([Score]) AS [totalScore]");
    }

    [Fact]
    public void BuildAggregateSelectParts_Avg_GeneratesAvg()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Avg, Field = "Score", Alias = "avgScore" }]
        };

        var parts = builder.BuildAggregateSelectParts(options, mapping);

        parts.Should().ContainSingle().Which.Should().Be("AVG([Score]) AS [avgScore]");
    }

    [Fact]
    public void BuildAggregateSelectParts_Min_GeneratesMin()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Min, Field = "Score", Alias = "minScore" }]
        };

        var parts = builder.BuildAggregateSelectParts(options, mapping);

        parts.Should().ContainSingle().Which.Should().Be("MIN([Score]) AS [minScore]");
    }

    [Fact]
    public void BuildAggregateSelectParts_Max_GeneratesMax()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Max, Field = "Score", Alias = "maxScore" }]
        };

        var parts = builder.BuildAggregateSelectParts(options, mapping);

        parts.Should().ContainSingle().Which.Should().Be("MAX([Score]) AS [maxScore]");
    }

    [Fact]
    public void BuildAggregateSelectParts_MultipleAggregates_ReturnsAll()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions
        {
            Aggregates =
            [
                new AggregateModel { Function = AggregateFunction.Count, Field = null, Alias = "total" },
                new AggregateModel { Function = AggregateFunction.Sum, Field = "Score", Alias = "sumScore" },
                new AggregateModel { Function = AggregateFunction.Avg, Field = "Score", Alias = "avgScore" }
            ]
        };

        var parts = builder.BuildAggregateSelectParts(options, mapping);

        parts.Should().HaveCount(3);
        parts[0].Should().Be("COUNT(1) AS [total]");
        parts[1].Should().Be("SUM([Score]) AS [sumScore]");
        parts[2].Should().Be("AVG([Score]) AS [avgScore]");
    }

    [Fact]
    public void BuildAggregateSelectParts_WithTableAlias_UsesAlias()
    {
        var registry = new MappingRegistry();
        registry.Entity<Employee>().ToTable("Employees").HasAlias("e");
        var mapping = registry.GetMapping(typeof(Employee));
        var builder = new SqlSelectBuilder(registry, Dialect);
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Sum, Field = "Score", Alias = "total" }]
        };

        var parts = builder.BuildAggregateSelectParts(options, mapping);

        parts.Should().ContainSingle().Which.Should().Be("SUM([e].[Score]) AS [total]");
    }

    // ── BuildSelectClause ────────────────────────────────────────────────

    [Fact]
    public void BuildSelectClause_NoSelect_FallsBackToAllColumns()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions();
        var tree = new SelectionNode();

        var result = builder.BuildSelectClause(options, mapping, string.Empty, tree);

        result.Should().Be("SELECT [Id] AS [Id], [Name] AS [Name], [ManagerId] AS [ManagerId], [Score] AS [Score], [Status] AS [Status]");
    }

    [Fact]
    public void BuildSelectClause_WithSelect_IncludesOnlySelectedFields()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions
        {
            Select = [new SelectNode { Field = "Name" }, new SelectNode { Field = "Score" }]
        };
        var tree = new SelectionNode();
        tree.GetOrAddChild("Name");
        tree.GetOrAddChild("Score");

        var result = builder.BuildSelectClause(options, mapping, string.Empty, tree);

        result.Should().Be("SELECT [Name] AS [Name], [Score] AS [Score]");
    }

    [Fact]
    public void BuildSelectClause_WithDistinct_IncludesDistinct()
    {
        var mapping = _registry.GetMapping(typeof(Customer));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions
        {
            Select = [new SelectNode { Field = "Name" }]
        };
        var tree = new SelectionNode();
        tree.GetOrAddChild("Name");

        var result = builder.BuildSelectClause(options, mapping, "DISTINCT", tree);

        result.Should().Be("SELECT DISTINCT [Name] AS [Name]");
    }

    [Fact]
    public void BuildSelectClause_WithGroupByAndAggregates_IncludesBoth()
    {
        var mapping = _registry.GetMapping(typeof(Customer));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions
        {
            GroupBy = ["Status"],
            Aggregates = [new AggregateModel { Function = AggregateFunction.Count, Field = null, Alias = "cnt" }]
        };
        var tree = new SelectionNode();

        var result = builder.BuildSelectClause(options, mapping, string.Empty, tree);

        result.Should().Be("SELECT [Status], COUNT(1) AS [cnt]");
    }

    [Fact]
    public void BuildSelectClause_WithGroupByOnly_ReturnsGroupByColumns()
    {
        var mapping = _registry.GetMapping(typeof(Customer));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions
        {
            GroupBy = ["Status", "Name"]
        };
        var tree = new SelectionNode();

        var result = builder.BuildSelectClause(options, mapping, string.Empty, tree);

        result.Should().Be("SELECT [Status], [Name]");
    }

    [Fact]
    public void BuildSelectClause_WithAlias_UsesAlias()
    {
        var mapping = _registry.GetMapping(typeof(Customer));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions();
        var tree = new SelectionNode();
        var nameNode = tree.GetOrAddChild("Name");
        nameNode.Alias = "UserName";

        var result = builder.BuildSelectClause(options, mapping, string.Empty, tree);

        result.Should().Be("SELECT [Name] AS [UserName]");
    }

    [Fact]
    public void BuildSelectClause_WithNavigation_IncludesNavigationColumns()
    {


        var mapping = _registry.GetMapping(typeof(Customer));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions();
        var tree = new SelectionNode();
        var ordersNode = tree.GetOrAddChild("Orders");
        ordersNode.MarkIncludeAllScalars();

        var result = builder.BuildSelectClause(options, mapping, string.Empty, tree);

        result.Should().Contain("SELECT");
        result.Should().Contain("[Orders].[Id] AS [Orders_Id]");
        result.Should().Contain("[Orders].[Number] AS [Orders_Number]");
        result.Should().Contain("[Orders].[Total] AS [Orders_Total]");
    }

    [Fact]
    public void BuildSelectClause_WithNavigationAndSpecificFields_IncludesOnlySelected()
    {

        var mapping = _registry.GetMapping(typeof(Customer));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions();
        var tree = new SelectionNode();
        var ordersNode = tree.GetOrAddChild("Orders");
        ordersNode.GetOrAddChild("Total");
        ordersNode.GetOrAddChild("Number");

        var result = builder.BuildSelectClause(options, mapping, string.Empty, tree);

        result.Should().Be("SELECT [Orders].[Total] AS [Orders_Total], [Orders].[Number] AS [Orders_Number]");
    }

    [Fact]
    public void BuildSelectClause_WithPostgreSql_UsesCorrectQuoting()
    {
        var mapping = _registry.GetMapping(typeof(Customer));
        var builder = new SqlSelectBuilder(_registry, new PostgreSqlDialect());
        var options = new QueryOptions();
        var tree = new SelectionNode();
        tree.GetOrAddChild("Name");

        var result = builder.BuildSelectClause(options, mapping, string.Empty, tree);

        result.Should().Be("SELECT \"Name\" AS \"Name\"");
    }

    [Fact]
    public void BuildSelectClause_WithMySql_UsesCorrectQuoting()
    {
        var mapping = _registry.GetMapping(typeof(Customer));
        var builder = new SqlSelectBuilder(_registry, new MySqlDialect());
        var options = new QueryOptions();
        var tree = new SelectionNode();
        tree.GetOrAddChild("Name");

        var result = builder.BuildSelectClause(options, mapping, string.Empty, tree);

        result.Should().Be("SELECT `Name` AS `Name`");
    }

    [Fact]
    public void BuildSelectClause_WithTableAlias_UsesAlias()
    {
        var mapping = _registry.GetMapping<Employee>();
        _registry.Entity<Employee>().ToTable("Employees").HasAlias("e");
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions();
        var tree = new SelectionNode();
        tree.GetOrAddChild("Name");

        var result = builder.BuildSelectClause(options, mapping, string.Empty, tree);

        result.Should().Be("SELECT [e].[Name] AS [Name]");
    }

    // ── BuildFlatSelectClause ────────────────────────────────────────────

    [Fact]
    public void BuildFlatSelectClause_NoNavPath_ReturnsRootColumns()
    {
       

        var mapping = _registry.GetMapping(typeof(Customer));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions
        {
            ProjectionMode = ProjectionMode.Flat,
            Select = [new SelectNode { Field = "Name" }, new SelectNode { Field = "Email" }]
        };
        var tree = new SelectionNode();
        tree.GetOrAddChild("Name");
        tree.GetOrAddChild("Email");

        var (selectClause, joinClause, flatJoins) = builder.BuildFlatSelectClause(options, mapping, string.Empty, tree);

        selectClause.Should().Be("SELECT [Customers].[Name] AS [Name], [Customers].[Email] AS [Email]");
        joinClause.Should().BeEmpty();
        flatJoins.Should().BeEmpty();
    }

    [Fact]
    public void BuildFlatSelectClause_WithNavigation_GeneratesJoins()
    {

        var mapping = _registry.GetMapping(typeof(Customer));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions
        {
            ProjectionMode = ProjectionMode.Flat,
            Select = [new SelectNode { Field = "Orders.Total" }]
        };
        var tree = new SelectionNode();
        var ordersNode = tree.GetOrAddChild("Orders");
        ordersNode.GetOrAddChild("Total");

        var (selectClause, joinClause, flatJoins) = builder.BuildFlatSelectClause(options, mapping, string.Empty, tree);

        selectClause.Should().Be("SELECT [Orders].[Total] AS [Total]");
        joinClause.Should().Contain("LEFT JOIN");
        joinClause.Should().Contain("[Orders]");
        flatJoins.Should().Contain("Orders");
    }

    [Fact]
    public void BuildFlatSelectClause_MultiLevel_GeneratesMultipleJoins()
    {

        var mapping = _registry.GetMapping(typeof(Customer));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions
        {
            ProjectionMode = ProjectionMode.Flat,
            Select = [new SelectNode { Field = "Orders.OrderItems.Sku" }]
        };
        var tree = new SelectionNode();
        var ordersNode = tree.GetOrAddChild("Orders");
        var itemsNode = ordersNode.GetOrAddChild("OrderItems");
        itemsNode.GetOrAddChild("Sku");

        var (selectClause, joinClause, flatJoins) = builder.BuildFlatSelectClause(options, mapping, string.Empty, tree);

        selectClause.Should().Be("SELECT [OrderItems].[Sku] AS [Sku]");
        joinClause.Should().Contain("LEFT JOIN [Orders]");
        joinClause.Should().Contain("LEFT JOIN [OrderItems]");
        flatJoins.Should().Contain("Orders");
        flatJoins.Should().Contain("OrderItems");
    }

    [Fact]
    public void BuildFlatSelectClause_FlatMixed_IncludesRootScalars()
    {
        
        var mapping = _registry.GetMapping(typeof(Customer));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions
        {
            ProjectionMode = ProjectionMode.FlatMixed,
            Select = [new SelectNode { Field = "Name" }, new SelectNode { Field = "Orders.Total" }]
        };
        var tree = new SelectionNode();
        tree.GetOrAddChild("Name");
        var ordersNode = tree.GetOrAddChild("Orders");
        ordersNode.GetOrAddChild("Total");

        var (selectClause, joinClause, flatJoins) = builder.BuildFlatSelectClause(options, mapping, string.Empty, tree);

        selectClause.Should().Contain("[Customers].[Name] AS [Name]");
        selectClause.Should().Contain("[Orders].[Total] AS [Total]");
        joinClause.Should().Contain("LEFT JOIN");
        flatJoins.Should().Contain("Orders");
    }

    [Fact]
    public void BuildFlatSelectClause_NoSelect_FallsBackToAllColumns()
    {

        var mapping = _registry.GetMapping(typeof(Customer));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions
        {
            ProjectionMode = ProjectionMode.Flat,
            Select = [new SelectNode { Field = "Orders.Total" }]
        };
        var tree = new SelectionNode();
        var ordersNode = tree.GetOrAddChild("Orders");
        ordersNode.GetOrAddChild("Total");

        var (selectClause, joinClause, flatJoins) = builder.BuildFlatSelectClause(options, mapping, string.Empty, tree);

        selectClause.Should().Be("SELECT [Orders].[Total] AS [Total]");
        joinClause.Should().Contain("LEFT JOIN");
        flatJoins.Should().Contain("Orders");
    }

    [Fact]
    public void BuildFlatSelectClause_BranchingNavPath_Throws()
    {

        var mapping = _registry.GetMapping(typeof(Customer));
        var builder = new SqlSelectBuilder(_registry, Dialect);
        var options = new QueryOptions
        {
            ProjectionMode = ProjectionMode.Flat
        };
        var tree = new SelectionNode();
        tree.GetOrAddChild("Orders").GetOrAddChild("Total");
        tree.GetOrAddChild("Address").GetOrAddChild("City");

        var act = () => builder.BuildFlatSelectClause(options, mapping, string.Empty, tree);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void BuildFlatSelectClause_WithPostgreSql_UsesCorrectQuoting()
    {

        var mapping = _registry.GetMapping(typeof(Customer));
        var builder = new SqlSelectBuilder(_registry, new PostgreSqlDialect());
        var options = new QueryOptions
        {
            ProjectionMode = ProjectionMode.Flat,
            Select = [new SelectNode { Field = "Orders.Total" }]
        };
        var tree = new SelectionNode();
        var ordersNode = tree.GetOrAddChild("Orders");
        ordersNode.GetOrAddChild("Total");

        var (selectClause, joinClause, flatJoins) = builder.BuildFlatSelectClause(options, mapping, string.Empty, tree);

        selectClause.Should().Contain("\"Orders\".\"Total\"");
        joinClause.Should().Contain("LEFT JOIN");
    }

    [Fact]
    public void BuildFlatSelectClause_WithMySql_UsesCorrectQuoting()
    {
      
        var mapping = _registry.GetMapping(typeof(Customer));
        var builder = new SqlSelectBuilder(_registry, new MySqlDialect());
        var options = new QueryOptions
        {
            ProjectionMode = ProjectionMode.Flat,
            Select = [new SelectNode { Field = "Orders.Total" }]
        };
        var tree = new SelectionNode();
        var ordersNode = tree.GetOrAddChild("Orders");
        ordersNode.GetOrAddChild("Total");

        var (selectClause, joinClause, flatJoins) = builder.BuildFlatSelectClause(options, mapping, string.Empty, tree);

        selectClause.Should().Contain("`Orders`.`Total`");
        joinClause.Should().Contain("LEFT JOIN");
    }
    
}

