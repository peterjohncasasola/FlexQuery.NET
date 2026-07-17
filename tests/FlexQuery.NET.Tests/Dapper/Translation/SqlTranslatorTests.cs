using FlexQuery.NET.Models;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Paging;
using FlexQuery.NET.Models.Projection;

namespace FlexQuery.NET.Tests.Dapper.Translation;

public class SqlTranslatorTests
{
    private readonly IMappingRegistry _registry = SharedFlexQueryModel.Instance.Registry;

    private static QueryOptions NoPaging(QueryOptions options)
    {
        options.Paging.Disabled = true;
        return options;
    }

    [Fact]
    public void Translate_EmptyFilter_GeneratesSelectAll()
    {
        var options = NoPaging(new QueryOptions());
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("SELECT");
        command.Sql.Should().Contain("FROM");
        command.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Translate_SimpleEqFilter_GeneratesParameterizedWhere()
    {
        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "Alice" }]
            }
        });
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("WHERE");
        command.Parameters.Should().ContainKey("@p0");
        command.Parameters["@p0"].Should().Be("Alice");
    }

    [Fact]
    public void Translate_InOperator_GeneratesInClause()
    {
        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Status", Operator = "in", Value = "Active,Pending" }]
            }
        });
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("IN");
        command.Parameters.Should().HaveCount(2);
    }

    [Fact]
    public void Translate_BetweenOperator_GeneratesBetweenClause()
    {
        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Age", Operator = "between", Value = "20,30" }]
            }
        });
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("BETWEEN");
        command.Parameters.Should().HaveCount(2);
        command.Parameters["@p0"].Should().Be(20);
        command.Parameters["@p1"].Should().Be(30);
    }

    [Fact]
    public void Translate_ContainsOperator_GeneratesLikeClause()
    {
        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "contains", Value = "John" }]
            }
        });
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("LIKE");
        command.Parameters.Should().ContainKey("@p0");
        command.Parameters["@p0"].Should().Be("%John%");
    }

    [Fact]
    public void Translate_Sorts_GeneratesOrderBy()
    {
        var options = NoPaging(new QueryOptions
        {
            Sort = [new SortNode { Field = "Name", Descending = true }]
        });
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("ORDER BY");
        command.Sql.Should().Contain("DESC");
    }

    [Fact]
    public void Translate_GroupBy_GeneratesGroupByClause()
    {
        var options = NoPaging(new QueryOptions
        {
            GroupBy = ["City"]
        });
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("GROUP BY");
    }

    [Fact]
    public void Translate_Aggregates_GeneratesAggregateSelect()
    {
        var options = NoPaging(new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Count, Alias = "TotalCount" }]
        });
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.TranslateAggregates(options);

        command.Sql.Should().Contain("COUNT(1) AS [TotalCount]");
    }

    [Fact]
    public void Translate_Paging_GeneratesOffsetFetch()
    {
        var options = new QueryOptions
        {
            Sort = { new SortNode { Field = "Id" } },
            Paging = { Page = 2, PageSize = 10 }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("ORDER BY");
        command.Sql.Should().Contain("OFFSET");
        command.Sql.Should().Contain("FETCH NEXT");
        command.Parameters.Should().ContainKey("@Offset");
        command.Parameters.Should().ContainKey("@PageSize");
    }

    [Fact]
    public void Translate_AndLogic_CombinesWithAnd()
    {
        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Logic = LogicOperator.And,
                Filters =
                [
                    new FilterCondition { Field = "City", Operator = "eq", Value = "NYC" },
                    new FilterCondition { Field = "Age", Operator = "gt", Value = "25" }
                ]
            }
        });
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("AND");
        command.Sql.Should().NotContain("OR");
    }

    [Fact]
    public void Translate_OrLogic_CombinesWithOr()
    {
        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Logic = LogicOperator.Or,
                Filters =
                [
                    new FilterCondition { Field = "City", Operator = "eq", Value = "NYC" },
                    new FilterCondition { Field = "City", Operator = "eq", Value = "LA" }
                ]
            }
        });
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("OR");
        command.Sql.Should().Contain("(");
    }

    [Fact]
    public void Translate_SelectFields_GeneratesColumnList()
    {
        var options = NoPaging(new QueryOptions
        {
            Select = [new SelectNode { Field = "Id" }, new SelectNode { Field = "Name" }, new SelectNode { Field = "Age" }]
        });
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("[Id]");
        command.Sql.Should().Contain("[Name]");
        command.Sql.Should().Contain("[Age]");
    }

    [Fact]
    public void Translate_Distinct_GeneratesDistinctClause()
    {
        var options = NoPaging(new QueryOptions
        {
            Distinct = true
        });
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("SELECT DISTINCT");
    }

    [Fact]
    public void Translate_Having_GeneratesHavingClause()
    {
        var options = NoPaging(new QueryOptions
        {
            GroupBy = ["Status"],
            Having = new HavingCondition
            {
                Field = "Amount",
                Operator = "gt",
                Value = "100",
                Function = AggregateFunction.Sum
            }
        });
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("GROUP BY");
        command.Sql.Should().Contain("HAVING");
        command.Sql.Should().Contain("SUM");
    }

    [Fact]
    public void Translate_Having_FieldLessCount_GeneratesCountStar()
    {
        var options = NoPaging(new QueryOptions
        {
            GroupBy = ["Status"],
            Aggregates = [new AggregateModel { Function = AggregateFunction.Count, Alias = "Count" }],
            Having = new HavingCondition
            {
                Function = AggregateFunction.Count,
                Operator = "gt",
                Value = "20"
            }
        });
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("GROUP BY");
        command.Sql.Should().Contain("HAVING");
        command.Sql.Should().Contain("COUNT(*)");
        command.Sql.Should().NotContain("[*]");
    }

    [Fact]
    public void Translate_Having_ClauseOrdering_GroupByBeforeHaving()
    {
        var options = NoPaging(new QueryOptions
        {
            GroupBy = ["Status"],
            Aggregates = [new AggregateModel { Function = AggregateFunction.Count, Alias = "Count" }],
            Having = new HavingCondition
            {
                Function = AggregateFunction.Count,
                Operator = "gt",
                Value = "20"
            }
        });
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        var groupByPos = command.Sql.IndexOf("GROUP BY", StringComparison.OrdinalIgnoreCase);
        var havingPos = command.Sql.IndexOf("HAVING", StringComparison.OrdinalIgnoreCase);

        groupByPos.Should().BeGreaterThanOrEqualTo(0);
        havingPos.Should().BeGreaterThan(groupByPos);
    }

    [Fact]
    public void Translate_Having_SelectBuildsAggregateSelectWithNewAlias()
    {
        var options = NoPaging(new QueryOptions
        {
            GroupBy = ["Status"],
            Aggregates = [new AggregateModel { Function = AggregateFunction.Count, Alias = "Count" }],
        });
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("COUNT(1) AS [Count]");
    }

    [Fact]
    public void Translate_Includes_GeneratesJoinClause()
    {
        var options = NoPaging(new QueryOptions
        {
            Includes = new List<string> { "Roles" }
        });
        options.Items[ContextKeys.EntityType] = typeof(User);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("LEFT JOIN");
    }

    [Fact]
    public void Translate_AnyOperator_GeneratesExistsSubquery()
    {
        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition 
                { 
                    Field = "Roles", 
                    Operator = "any", 
                    ScopedFilter = new FilterGroup 
                    { 
                        Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "Admin" }] 
                    } 
                }]
            }
        });
        options.Items[ContextKeys.EntityType] = typeof(User);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("EXISTS");
        command.Sql.Should().Contain("SELECT 1 FROM [Roles]");
        command.Sql.Should().Contain("[Roles].[UserId] = [Users].[Id]");
        command.Sql.Should().Contain("LOWER([Name]) = LOWER(@p0)");
        command.Parameters["@p0"].Should().Be("Admin");
    }

    [Fact]
    public void Translate_AllOperator_GeneratesNotExistsSubquery()
    {
        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition 
                { 
                    Field = "Roles", 
                    Operator = "all", 
                    ScopedFilter = new FilterGroup 
                    { 
                        Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "User" }] 
                    } 
                }]
            }
        });
        options.Items[ContextKeys.EntityType] = typeof(User);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("NOT EXISTS");
        command.Sql.Should().Contain("SELECT 1 FROM [Roles]");
        command.Sql.Should().Contain("NOT (LOWER([Name]) = LOWER(@p0))");
    }

    [Fact]
    public void Translate_CountOperator_GeneratesCorrelatedCountSubquery()
    {
        var options = NoPaging(new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition 
                { 
                    Field = "Roles", 
                    Operator = "count", 
                    Value = "gt:5",
                    ScopedFilter = new FilterGroup 
                    { 
                        Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "Guest" }] 
                    } 
                }]
            }
        });
        options.Items[ContextKeys.EntityType] = typeof(User);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("(SELECT COUNT(*) FROM [Roles]");
        command.Sql.Should().Contain("[Roles].[UserId] = [Users].[Id]");
        command.Sql.Should().Contain("> @p1");
        command.Parameters["@p1"].Should().Be(5);
    }

    [Fact]
    public void Translate_FilteredInclude_GeneratesJoinWithFilter()
    {
        var options = NoPaging(new QueryOptions
        {
            Expand = 
            [
                new IncludeNode 
                { 
                    Path = "Roles", 
                    Filter = new FilterGroup 
                    { 
                        Filters = [new FilterCondition { Field = "IsActive", Operator = "eq", Value = "true" }] 
                    } 
                }
            ]
        });
        options.Items[ContextKeys.EntityType] = typeof(User);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("LEFT JOIN [Roles]");
        command.Sql.Should().Contain("[Roles].[UserId] = [Users].[Id]");
        command.Sql.Should().Contain("AND ([IsActive] = @p0)");
    }
    

    [Fact]
    public void Translate_FlatMode_GeneratesFlatJoins()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = new QueryOptions
        {
            ProjectionMode = ProjectionMode.Flat,
            Select = [new SelectNode { Field = "Orders.Total" }],
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var translator = new SqlTranslator(registry, new SqliteDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("LEFT JOIN");
        command.Sql.Should().Contain("\"Orders\"");
        command.FlatJoins.Should().Contain("Orders");
    }

    [Fact]
    public void Translate_FlatMixedMode_IncludesRootScalars()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = new QueryOptions
        {
            ProjectionMode = ProjectionMode.FlatMixed,
            Select = [new SelectNode { Field = "Name" }, new SelectNode { Field = "Orders.Total" }],
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var translator = new SqlTranslator(registry, new SqliteDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("\"Customers\".\"Name\"");
        command.Sql.Should().Contain("\"Orders\".\"Total\"");
        command.FlatJoins.Should().Contain("Orders");
    }

    [Fact]
    public void Translate_FlatMode_MultiLevel_NestedCollection()
    {
        var registry = SharedFlexQueryModel.Instance.Registry;

        var options = new QueryOptions
        {
            ProjectionMode = ProjectionMode.Flat,
            Select = [new SelectNode { Field = "Orders.OrderItems.Sku" }, new SelectNode { Field = "Orders.OrderItems.Id" }],
            Paging = { Disabled = true }
        };
        options.Items[ContextKeys.EntityType] = typeof(Customer);

        var translator = new SqlTranslator(registry, new SqliteDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("LEFT JOIN");
        command.Sql.Should().Contain("\"Orders\"");
        command.Sql.Should().Contain("\"OrderItems\"");
        command.Sql.Should().Contain("\"OrderItems\".\"Sku\"");
        command.Sql.Should().Contain("\"OrderItems\".\"Id\"");
        command.FlatJoins.Should().Contain("Orders");
        command.FlatJoins.Should().Contain("OrderItems");
    }
}

