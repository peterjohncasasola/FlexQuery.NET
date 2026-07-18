using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql.Builders;
using FlexQuery.NET.Dapper.Sql.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;

namespace FlexQuery.NET.Tests.Dapper.Translation;

public class SqlHavingBuilderTests
{
    private readonly IMappingRegistry _registry = SharedFlexQueryModel.Instance.Registry;
    private static readonly ISqlDialect Dialect = new SqlServerDialect();

    public SqlHavingBuilderTests()
    {
        _registry.Entity<Employee>().ToTable("Employees");
    }

    [Fact]
    public void Build_NullHaving_ReturnsEmpty()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);
        var result = SqlHavingBuilder.Build(Dialect, null, mapping, parameters);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Build_CountStarWithGt_GeneratesHavingCountGt()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);
        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Count,
            Field = null,
            Operator = "gt",
            Value = "5"
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);

        result.Should().Be("HAVING COUNT(*) > @p0");
        parameters.Parameters.Should().ContainKey("@p0");
        parameters.Parameters["@p0"].Should().Be(5L);
    }

    [Fact]
    public void Build_CountStarWithEq_GeneratesHavingCountEq()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);
        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Count,
            Field = null,
            Operator = "eq",
            Value = "10"
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);

        result.Should().Be("HAVING COUNT(*) = @p0");
        parameters.Parameters["@p0"].Should().Be(10L);
    }

    [Fact]
    public void Build_CountStarWithLt_GeneratesHavingCountLt()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);
        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Count,
            Field = null,
            Operator = "lt",
            Value = "100"
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);

        result.Should().Be("HAVING COUNT(*) < @p0");
        parameters.Parameters["@p0"].Should().Be(100L);
    }

    [Fact]
    public void Build_CountStarWithGte_GeneratesHavingCountGte()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);
        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Count,
            Field = null,
            Operator = "gte",
            Value = "0"
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);

        result.Should().Be("HAVING COUNT(*) >= @p0");
        parameters.Parameters["@p0"].Should().Be(0L);
    }

    [Fact]
    public void Build_CountStarWithLte_GeneratesHavingCountLte()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);
        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Count,
            Field = null,
            Operator = "lte",
            Value = "50"
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);

        result.Should().Be("HAVING COUNT(*) <= @p0");
        parameters.Parameters["@p0"].Should().Be(50L);
    }

    [Fact]
    public void Build_CountStarWithNeq_GeneratesHavingCountNeq()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);
        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Count,
            Field = null,
            Operator = "neq",
            Value = "0"
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);

        result.Should().Be("HAVING COUNT(*) <> @p0");
        parameters.Parameters["@p0"].Should().Be(0L);
    }

    [Fact]
    public void Build_CountField_GeneratesHavingCountField()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);
        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Count,
            Field = "Status",
            Operator = "gt",
            Value = "3"
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);

        result.Should().Be("HAVING COUNT([Status]) > @p0");
        parameters.Parameters["@p0"].Should().Be("3");
    }

    [Fact]
    public void Build_SumField_GeneratesHavingSum()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);
        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Sum,
            Field = "Score",
            Operator = "gt",
            Value = "100"
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);

        result.Should().Be("HAVING SUM([Score]) > @p0");
        parameters.Parameters["@p0"].Should().Be(100L);
    }

    [Fact]
    public void Build_AvgField_GeneratesHavingAvg()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);
        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Avg,
            Field = "Score",
            Operator = "gte",
            Value = "75.5"
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);

        result.Should().Be("HAVING AVG([Score]) >= @p0");
        parameters.Parameters["@p0"].Should().Be(75.5);
    }

    [Fact]
    public void Build_MinField_GeneratesHavingMin()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);
        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Min,
            Field = "Score",
            Operator = "lt",
            Value = "10"
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);

        result.Should().Be("HAVING MIN([Score]) < @p0");
        parameters.Parameters["@p0"].Should().Be(10L);
    }

    [Fact]
    public void Build_MaxField_GeneratesHavingMax()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);
        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Max,
            Field = "Score",
            Operator = "lte",
            Value = "200"
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);

        result.Should().Be("HAVING MAX([Score]) <= @p0");
        parameters.Parameters["@p0"].Should().Be(200L);
    }

    [Fact]
    public void Build_OperatorAliases_NormalizeCorrectly()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);

        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Count,
            Field = null,
            Operator = "=",
            Value = "5"
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);
        result.Should().Be("HAVING COUNT(*) = @p0");
    }

    [Fact]
    public void Build_OperatorNeqAliases_NormalizeCorrectly()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);

        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Count,
            Field = null,
            Operator = "!=",
            Value = "5"
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);
        result.Should().Be("HAVING COUNT(*) <> @p0");
    }

    [Fact]
    public void Build_OperatorGtAliases_NormalizeCorrectly()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);

        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Count,
            Field = null,
            Operator = "greaterthan",
            Value = "5"
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);
        result.Should().Be("HAVING COUNT(*) > @p0");
    }

    [Fact]
    public void Build_OperatorLtAliases_NormalizeCorrectly()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);

        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Count,
            Field = null,
            Operator = "lessthan",
            Value = "5"
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);
        result.Should().Be("HAVING COUNT(*) < @p0");
    }

    [Fact]
    public void Build_OperatorGteAliases_NormalizeCorrectly()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);

        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Count,
            Field = null,
            Operator = ">=",
            Value = "5"
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);
        result.Should().Be("HAVING COUNT(*) >= @p0");
    }

    [Fact]
    public void Build_OperatorLteAliases_NormalizeCorrectly()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);

        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Count,
            Field = null,
            Operator = "<=",
            Value = "5"
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);
        result.Should().Be("HAVING COUNT(*) <= @p0");
    }

    [Fact]
    public void Build_SumWithDecimalValue_ConvertsCorrectly()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);
        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Sum,
            Field = "Score",
            Operator = "gt",
            Value = "99.99"
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);

        result.Should().Be("HAVING SUM([Score]) > @p0");
        parameters.Parameters["@p0"].Should().Be(99.99);
    }

    [Fact]
    public void Build_CountStarWithSqlite_ConvertsDecimalToDouble()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(new SqliteDialect());
        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Count,
            Field = null,
            Operator = "gt",
            Value = "5"
        };

        var result = SqlHavingBuilder.Build(new SqliteDialect(), having, mapping, parameters);

        result.Should().Be("HAVING COUNT(*) > @p0");
        parameters.Parameters["@p0"].Should().Be(5L);
    }

    [Fact]
    public void Build_SumWithSqlite_ConvertsDecimalToDouble()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(new SqliteDialect());
        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Sum,
            Field = "Score",
            Operator = "gt",
            Value = "99.99"
        };

        var result = SqlHavingBuilder.Build(new SqliteDialect(), having, mapping, parameters);

        result.Should().Be("HAVING SUM(\"Score\") > @p0");
        parameters.Parameters["@p0"].Should().Be(99.99);
    }

    [Fact]
    public void Build_WithPostgreSql_UsesCorrectQuoting()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(new PostgreSqlDialect());
        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Sum,
            Field = "Score",
            Operator = "gt",
            Value = "100"
        };

        var result = SqlHavingBuilder.Build(new PostgreSqlDialect(), having, mapping, parameters);

        result.Should().Be("HAVING SUM(\"Score\") > :p0");
    }

    [Fact]
    public void Build_WithMySql_UsesCorrectQuoting()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(new MySqlDialect());
        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Sum,
            Field = "Score",
            Operator = "gt",
            Value = "100"
        };

        var result = SqlHavingBuilder.Build(new MySqlDialect(), having, mapping, parameters);

        result.Should().Be("HAVING SUM(`Score`) > ?p0");
    }

    [Fact]
    public void Build_WithTableAlias_UsesAliasInColumn()
    {
        var registry = new MappingRegistry();
        registry.Entity<Employee>().ToTable("Employees").HasAlias("e");
        var mapping = registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);
        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Sum,
            Field = "Score",
            Operator = "gt",
            Value = "100"
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);

        result.Should().Be("HAVING SUM([e].[Score]) > @p0");
    }

    [Fact]
    public void Build_UnknownOperator_ReturnsRawOperator()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);
        var having = new HavingConditionNode
        {
            Function = AggregateFunction.Count,
            Field = null,
            Operator = "custom_op",
            Value = "5"
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);

        result.Should().Be("HAVING COUNT(*) custom_op @p0");
    }

    [Fact]
    public void Build_OrLogicalNode_GeneratesOrSql()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);

        var having = new HavingLogicalNode
        {
            Logic = LogicOperator.Or,
            Children =
            [
                new HavingConditionNode
                {
                    Function = AggregateFunction.Count,
                    Field = null,
                    Operator = "eq",
                    Value = "627"
                },
                new HavingConditionNode
                {
                    Function = AggregateFunction.Avg,
                    Field = "Score",
                    Operator = "lte",
                    Value = "25000"
                }
            ]
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);

        result.Should().Be("HAVING (COUNT(*) = @p0 OR AVG([Score]) <= @p1)");
    }

    [Fact]
    public void Build_AndLogicalNode_GeneratesAndSql()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);

        var having = new HavingLogicalNode
        {
            Logic = LogicOperator.And,
            Children =
            [
                new HavingConditionNode
                {
                    Function = AggregateFunction.Count,
                    Field = null,
                    Operator = "eq",
                    Value = "627"
                },
                new HavingConditionNode
                {
                    Function = AggregateFunction.Avg,
                    Field = "Score",
                    Operator = "lte",
                    Value = "25000"
                }
            ]
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);

        result.Should().Be("HAVING (COUNT(*) = @p0 AND AVG([Score]) <= @p1)");
    }

    [Fact]
    public void Build_NestedLogicalNode_GeneratesNestedParentheses()
    {
        var mapping = _registry.GetMapping(typeof(Employee));
        var parameters = new SqlParameterContext(Dialect);

        var having = new HavingLogicalNode
        {
            Logic = LogicOperator.And,
            Children =
            [
                new HavingGroupNode
                {
                    Inner = new HavingLogicalNode
                    {
                        Logic = LogicOperator.Or,
                        Children =
                        [
                            new HavingConditionNode
                            {
                                Function = AggregateFunction.Count,
                                Field = null,
                                Operator = "eq",
                                Value = "627"
                            },
                            new HavingConditionNode
                            {
                                Function = AggregateFunction.Avg,
                                Field = "Score",
                                Operator = "lte",
                                Value = "25000"
                            }
                        ]
                    }
                },
                new HavingConditionNode
                {
                    Function = AggregateFunction.Sum,
                    Field = "Score",
                    Operator = "gt",
                    Value = "1000000"
                }
            ]
        };

        var result = SqlHavingBuilder.Build(Dialect, having, mapping, parameters);

        result.Should().Be("HAVING (((COUNT(*) = @p0 OR AVG([Score]) <= @p1)) AND SUM([Score]) > @p2)");
    }
    
}

