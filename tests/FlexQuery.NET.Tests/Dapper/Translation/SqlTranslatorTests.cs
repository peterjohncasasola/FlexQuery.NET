using FlexQuery.NET.Models;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Dapper.Dialects;
using FluentAssertions;

namespace FlexQuery.NET.Tests.Dapper.Translation;

public class SqlTranslatorTests
{
    private readonly IMappingRegistry _registry = new MappingRegistry();

    public SqlTranslatorTests()
    {
        var entityWithJoin = new EntityMapping(typeof(TestEntityWithJoin), "users", null);
        entityWithJoin.MapJoin("Roles", typeof(object), "roles", "users.Id = roles.UserId");
        ((MappingRegistry)_registry).Register(entityWithJoin);
    }

    private static QueryOptions NoPaging(QueryOptions options)
    {
        options.Paging.Disabled = true;
        return options;
    }

    [Fact]
    public void Translate_EmptyFilter_GeneratesSelectAll()
    {
        var options = NoPaging(new QueryOptions());
        options.Items["EntityType"] = typeof(TestEntity);

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
        options.Items["EntityType"] = typeof(TestEntity);

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
        options.Items["EntityType"] = typeof(TestEntity);

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
        options.Items["EntityType"] = typeof(TestEntity);

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
        options.Items["EntityType"] = typeof(TestEntity);

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
        options.Items["EntityType"] = typeof(TestEntity);

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
        options.Items["EntityType"] = typeof(TestEntity);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("GROUP BY");
    }

    [Fact]
    public void Translate_Aggregates_GeneratesAggregateSelect()
    {
        var options = NoPaging(new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = "count", Alias = "TotalCount" }]
        });
        options.Items["EntityType"] = typeof(TestEntity);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("COUNT(1) AS [TotalCount]");
    }

    [Fact]
    public void Translate_Paging_GeneratesOffsetFetch()
    {
        var options = new QueryOptions
        {
            Paging = { Page = 2, PageSize = 10 }
        };
        options.Items["EntityType"] = typeof(TestEntity);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

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
        options.Items["EntityType"] = typeof(TestEntity);

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
        options.Items["EntityType"] = typeof(TestEntity);

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
            Select = ["Id", "Name", "Age"]
        });
        options.Items["EntityType"] = typeof(TestEntity);

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
        options.Items["EntityType"] = typeof(TestEntity);

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
                Function = "sum"
            }
        });
        options.Items["EntityType"] = typeof(TestEntity);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("GROUP BY");
        command.Sql.Should().Contain("HAVING");
        command.Sql.Should().Contain("SUM");
    }

    [Fact]
    public void Translate_Includes_GeneratesJoinClause()
    {
        var options = NoPaging(new QueryOptions
        {
            Includes = new List<string> { "Roles" }
        });
        options.Items["EntityType"] = typeof(TestEntityWithJoin);

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
        options.Items["EntityType"] = typeof(TestEntityWithJoin);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("EXISTS");
        command.Sql.Should().Contain("SELECT 1 FROM [roles]");
        command.Sql.Should().Contain("users.Id = roles.UserId");
        command.Sql.Should().Contain("[Name] = @p0");
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
        options.Items["EntityType"] = typeof(TestEntityWithJoin);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("NOT EXISTS");
        command.Sql.Should().Contain("SELECT 1 FROM [roles]");
        command.Sql.Should().Contain("NOT ([Name] = @p0)");
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
        options.Items["EntityType"] = typeof(TestEntityWithJoin);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("(SELECT COUNT(*) FROM [roles]");
        command.Sql.Should().Contain("users.Id = roles.UserId");
        command.Sql.Should().Contain("> @p1");
        command.Parameters["@p1"].Should().Be(5);
    }

    [Fact]
    public void Translate_FilteredInclude_GeneratesJoinWithFilter()
    {
        var options = NoPaging(new QueryOptions
        {
            FilteredIncludes = 
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
        options.Items["EntityType"] = typeof(TestEntityWithJoin);

        var translator = new SqlTranslator(_registry, new SqlServerDialect());
        var command = translator.Translate(options);

        command.Sql.Should().Contain("LEFT JOIN [roles]");
        command.Sql.Should().Contain("users.Id = roles.UserId");
        command.Sql.Should().Contain("AND ([IsActive] = @p0)");
    }

    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string City { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    private class TestEntityWithJoin
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
