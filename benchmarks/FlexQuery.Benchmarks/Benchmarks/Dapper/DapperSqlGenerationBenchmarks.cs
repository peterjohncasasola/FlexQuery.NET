using BenchmarkDotNet.Attributes;
using FlexQuery.NET.Dapper;
using FlexQuery.NET;
using FlexQuery.NET.Dapper.Dialects;
using FlexQuery.NET.Dapper.Mapping;
using FlexQuery.NET.Dapper.Sql;
using FlexQuery.NET.Dapper.Sql.Translators;
using FlexQuery.NET.Models;
using System.Collections.Generic;
using System.Data.Common;

namespace FlexQuery.Benchmarks.Benchmarks.Dapper;

[Config(typeof(Infrastructure.CustomBenchmarkConfig))]
public class DapperSqlGenerationBenchmarks
{
    private class SqlEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Age { get; set; }
        public string City { get; set; } = string.Empty;
        public decimal Salary { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    private IMappingRegistry _registry = null!;
    private ISqlDialect _dialect = null!;
    private SqlTranslator _translator = null!;
    private QueryOptions _simpleOptions = null!;
    private QueryOptions _complexFilterOptions = null!;
    private QueryOptions _aggregateOptions = null!;

    [GlobalSetup]
    public void Setup()
    {
        _registry = new MappingRegistry();
        _registry.Entity<SqlEntity>().ToTable("SqlEntities");

        _dialect = new SqlServerDialect();
        _translator = new SqlTranslator(_registry, _dialect);

        _simpleOptions = BuildSimpleQuery();
        _complexFilterOptions = BuildComplexFilterQuery();
        _aggregateOptions = BuildAggregateQuery();
    }

    [Benchmark]
    public SqlCommand SqlGeneration_Simple_10_Fields()
    {
        return _translator.Translate(_simpleOptions);
    }

    [Benchmark]
    public SqlCommand SqlGeneration_Complex_Filter()
    {
        return _translator.Translate(_complexFilterOptions);
    }

    [Benchmark]
    public SqlCommand SqlGeneration_Aggregates()
    {
        return _translator.Translate(_aggregateOptions);
    }

    private static QueryOptions BuildSimpleQuery()
    {
        var options = new QueryOptions
        {
            Select = ["Id", "Name", "Email", "Status", "Age", "City", "Salary", "CreatedAt", "IsActive", "Description"],
            Items = { ["EntityType"] = typeof(SqlEntity) }
        };
        options.Sort.Add(new SortNode { Field = "Name", Descending = false });
        options.Paging.Page = 1;
        options.Paging.PageSize = 20;
        return options;
    }

    private static QueryOptions BuildComplexFilterQuery()
    {
        var options = new QueryOptions
        {
            Select = ["Id", "Name", "Email"],
            Items = { ["EntityType"] = typeof(SqlEntity) }
        };
        options.Filter = new FilterGroup
        {
            Logic = LogicOperator.And,
            Filters =
            [
                new FilterCondition { Field = "Status", Operator = "eq", Value = "active" },
                new FilterCondition { Field = "Age", Operator = "gt", Value = "25" },
                new FilterCondition { Field = "City", Operator = "eq", Value = "New York" },
                new FilterCondition { Field = "Salary", Operator = "gte", Value = "50000" },
                new FilterCondition { Field = "CreatedAt", Operator = "gte", Value = "2024-01-01" },
            ]
        };
        options.Sort.Add(new SortNode { Field = "Name", Descending = false });
        options.Sort.Add(new SortNode { Field = "Age", Descending = true });
        options.Paging.Page = 1;
        options.Paging.PageSize = 50;
        return options;
    }

    private static QueryOptions BuildAggregateQuery()
    {
        var options = new QueryOptions
        {
            GroupBy = ["Status"],
            Aggregates =
            [
                new AggregateModel { Field = "Salary", Function = "avg", Alias = "avgSalary" },
                new AggregateModel { Field = "Salary", Function = "sum", Alias = "totalSalary" },
                new AggregateModel { Field = "Age", Function = "max", Alias = "maxAge" },
                new AggregateModel { Field = "Id", Function = "count", Alias = "count" },
            ],
            Items = { ["EntityType"] = typeof(SqlEntity) }
        };
        return options;
    }
}
