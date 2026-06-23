using System.Reflection;
using FlexQuery.NET.Dapper;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Models;
using FluentAssertions;

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
            Function = "sum",
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
            Function = "sum",
            Operator = FilterOperators.GreaterThan,
            Value = "100"
        };
        options.Sort = [new SortNode { Field = "totalSum", Descending = true }];
        options.Paging = new PagingOptions { Page = 2, PageSize = 10 };

        var command = Translate(options);
        var countSql = ExtractCountSql(command.Sql);

        countSql.Should().StartWith("SELECT COUNT(1) FROM (SELECT");
        countSql.Should().Contain("GROUP BY");
        countSql.Should().Contain("HAVING");
        countSql.Should().NotContain("ORDER BY");
        countSql.Should().NotContain("LIMIT");
        countSql.Should().NotContain("OFFSET");
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
                    Function = "sum",
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
