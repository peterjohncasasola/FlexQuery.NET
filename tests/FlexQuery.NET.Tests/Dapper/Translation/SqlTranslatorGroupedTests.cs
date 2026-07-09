using System.Reflection;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql;
using FlexQuery.NET.Dapper.Sql.Builders;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;

namespace FlexQuery.NET.Tests.Dapper.Translation;

public class SqlTranslatorGroupedTests
{
    private readonly MappingRegistry _registry = CreateRegistry();

    [Fact]
    public void Dapper_GroupedSort_ByAggregateAlias_GeneratesValidOrderBy()
    {
        var options = GroupedOptions();
        options.Sort = [new SortNode { Field = "totalSum", Descending = true }];

        var command = Translate(options);

        command.Sql.Should().Contain("SUM(\"Total\") AS \"totalSum\"");
        command.Sql.Should().Contain("ORDER BY \"totalSum\" DESC");
        command.Sql.Should().NotContain("ORDER BY \"Orders\".\"totalSum\" DESC");
    }

    [Fact]
    public void Dapper_GroupedMultiSort_AppliesOrderByThenBy()
    {
        var options = GroupedOptions();
        options.Sort =
        [
            new SortNode { Field = "CustomerId" },
            new SortNode { Field = "totalSum", Descending = true }
        ];

        var command = Translate(options);

        command.Sql.Should().Contain("ORDER BY \"CustomerId\", \"totalSum\" DESC");
    }

    [Fact]
    public void Dapper_GroupedPaging_GeneratesCorrectClauseOrdering()
    {
        var options = GroupedOptions();
        options.Filter = new FilterGroup
        {
            Filters =
            [
                new FilterCondition
                {
                    Field = "Number",
                    Operator = FilterOperators.StartsWith,
                    Value = "GROUP-"
                }
            ]
        };
        options.Having = new HavingCondition
        {
            Field = "Total",
            Function = AggregateFunction.Sum,
            Operator = FilterOperators.GreaterThan,
            Value = "100"
        };
        options.Sort = [new SortNode { Field = "totalSum", Descending = true }];
        options.Paging = new PagingOptions { Page = 2, PageSize = 10 };

        var command = Translate(options);

        AssertAppearsBefore(command.Sql, "WHERE", "GROUP BY");
        AssertAppearsBefore(command.Sql, "GROUP BY", "HAVING");
        AssertAppearsBefore(command.Sql, "HAVING", "ORDER BY");
        AssertAppearsBefore(command.Sql, "ORDER BY", "LIMIT");
        AssertAppearsBefore(command.Sql, "LIMIT", "OFFSET");
    }

    [Fact]
    public void Dapper_GroupedQuery_CountSql_CountsGroupedRowsForFutureResultCount()
    {
        var options = GroupedOptions();
        options.Filter = new FilterGroup
        {
            Filters =
            [
                new FilterCondition
                {
                    Field = "Number",
                    Operator = FilterOperators.StartsWith,
                    Value = "GROUP-"
                }
            ]
        };
        options.Having = new HavingCondition
        {
            Field = "Total",
            Function = AggregateFunction.Sum,
            Operator = FilterOperators.GreaterThan,
            Value = "100"
        };
        options.Sort = [new SortNode { Field = "totalSum", Descending = true }];
        options.Paging = new PagingOptions { Page = 2, PageSize = 10 };

        var command = Translate(options);
        var countSql = SqlCountBuilder.ExtractCountSql(command.Sql);

        countSql.Should().StartWith("SELECT COUNT(1) FROM (SELECT");
        countSql.Should().Contain("GROUP BY");
        countSql.Should().Contain("HAVING");
        countSql.Should().NotContain("ORDER BY");
        countSql.Should().NotContain("LIMIT");
        countSql.Should().NotContain("OFFSET");
    }

    // ========================
    // ExtractCountSql Regression Tests
    // ========================

    [Fact]
    public void ExtractCountSql_TopLevelOrderBy_IsStripped()
    {
        var sql = "SELECT Id, Name FROM Users ORDER BY Id";
        var countSql =  SqlCountBuilder.ExtractCountSql(sql);
        countSql.Should().Be("SELECT COUNT(1) FROM (SELECT Id, Name FROM Users) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_TopLevelOrderByAndLimit_AreStripped()
    {
        var sql = "SELECT * FROM Orders ORDER BY CreatedAt DESC LIMIT 10";
        var countSql = SqlCountBuilder.ExtractCountSql(sql);
        countSql.Should().Be("SELECT COUNT(1) FROM (SELECT * FROM Orders) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_TopLevelOrderByOffsetFetch_AreStripped()
    {
        var sql = "SELECT Id, Name FROM Users ORDER BY Name OFFSET 10 ROWS FETCH NEXT 20 ROWS ONLY";
        var countSql = SqlCountBuilder.ExtractCountSql(sql);
        countSql.Should().Be("SELECT COUNT(1) FROM (SELECT Id, Name FROM Users) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_OrderByInsideSubquery_IsPreserved()
    {
        var sql = "SELECT * FROM (SELECT * FROM Orders ORDER BY Id) AS sub OFFSET 20 ROWS FETCH NEXT 10 ROWS ONLY";
        var countSql = SqlCountBuilder.ExtractCountSql(sql);
        countSql.Should().Be("SELECT COUNT(1) FROM (SELECT * FROM (SELECT * FROM Orders ORDER BY Id) AS sub) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_LimitInsideSubquery_IsPreserved()
    {
        var sql = "SELECT * FROM (SELECT * FROM Items LIMIT 5) AS topItems WHERE Active = 1 ORDER BY Name LIMIT 10";
        var countSql = SqlCountBuilder.ExtractCountSql(sql);
        countSql.Should().Be("SELECT COUNT(1) FROM (SELECT * FROM (SELECT * FROM Items LIMIT 5) AS topItems WHERE Active = 1) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_OffsetInsideSubquery_IsPreserved()
    {
        var sql = "SELECT * FROM (SELECT * FROM Orders OFFSET 5 ROWS FETCH NEXT 5 ROWS ONLY) AS page WHERE Total > 100 ORDER BY Total";
        var countSql = SqlCountBuilder.ExtractCountSql(sql);
        countSql.Should().Be("SELECT COUNT(1) FROM (SELECT * FROM (SELECT * FROM Orders OFFSET 5 ROWS FETCH NEXT 5 ROWS ONLY) AS page WHERE Total > 100) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_NoPagingClauses_ReturnsWrappedSql()
    {
        var sql = "SELECT Id, Name FROM Users WHERE Active = 1";
        var countSql = SqlCountBuilder.ExtractCountSql(sql);
        countSql.Should().Be("SELECT COUNT(1) FROM (SELECT Id, Name FROM Users WHERE Active = 1) AS CountTable");
    }

    [Fact]
    public void ExtractCountSql_KeywordInAlias_DoesNotTriggerFalsePositive()
    {
        var sql = "SELECT MAX(Price) AS maxPrice, MIN(Price) AS minPrice, SUM(Quantity) AS totalOrdered FROM Orders ORDER BY totalOrdered";
        var countSql =SqlCountBuilder.ExtractCountSql(sql);
        countSql.Should().NotContain("ORDER BY");
    }

    [Fact]
    public void ExtractCountSql_DeeplyNestedSubqueries_OnlyStripsTopLevelClauses()
    {
        var sql = "SELECT * FROM (SELECT * FROM T1 ORDER BY Id LIMIT 10) AS sub WHERE Active = 1 ORDER BY Name";
        var countSql =SqlCountBuilder.ExtractCountSql(sql);
        countSql.Should().Contain("T1 ORDER BY Id LIMIT 10");
        countSql.Should().NotContain("ORDER BY Name");
    }

    private SqlCommand Translate(QueryOptions options)
    {
        options.Items[ContextKeys.EntityType] = typeof(SqlOrder);
        return new SqlTranslator(_registry, new SqliteDialect()).Translate(options);
    }

    private static QueryOptions GroupedOptions()
    {
        return new QueryOptions
        {
            GroupBy = ["CustomerId"],
            Aggregates =
            [
                new AggregateModel
                {
                    Field = "Total",
                    Function = AggregateFunction.Sum,
                    Alias = "totalSum"
                }
            ],
            Paging = { Disabled = true }
        };
    }

    private static void AssertAppearsBefore(string sql, string earlier, string later)
    {
        var earlierIndex = sql.IndexOf(earlier, StringComparison.OrdinalIgnoreCase);
        var laterIndex = sql.IndexOf(later, StringComparison.OrdinalIgnoreCase);

        earlierIndex.Should().BeGreaterThanOrEqualTo(0, $"SQL should contain {earlier}: {sql}");
        laterIndex.Should().BeGreaterThanOrEqualTo(0, $"SQL should contain {later}: {sql}");
        earlierIndex.Should().BeLessThan(laterIndex, $"{earlier} should appear before {later}: {sql}");
    }

    private static string ExtractCountSql(string sql)
    {
        var method = typeof(FlexQueryDapperExtensions).GetMethod(
            "ExtractCountSql",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull("the Dapper provider should expose its internal count SQL strategy for regression coverage");
        return (string)method!.Invoke(null, [sql])!;
    }

    private static MappingRegistry CreateRegistry()
    {
        var registry = new MappingRegistry();
        registry.Entity<SqlOrder>()
            .ToTable("Orders")
            .HasMany(o => o.Items).WithForeignKey("OrderId");
        registry.Entity<SqlOrderItem>().ToTable("OrderItems");
        return registry;
    }
}
