using BenchmarkDotNet.Attributes;
using FlexQuery.NET;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using System.Collections.Generic;
using System.Linq;

namespace FlexQuery.Benchmarks.Benchmarks.Validation;

[Config(typeof(Infrastructure.CustomBenchmarkConfig))]
public class ValidationBenchmarks
{
    private class TestEntity
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

    private QueryOptions _options10 = null!;
    private QueryOptions _options100 = null!;
    private QueryOptions _options1000 = null!;
    private QueryExecutionOptions _securityOptions = null!;

    [GlobalSetup]
    public void Setup()
    {
        _securityOptions = new QueryExecutionOptions
        {
            AllowedFields = new HashSet<string> { "Id", "Name", "Email", "Status", "Age", "City", "Salary", "CreatedAt", "IsActive", "Description" },
            BlockedFields = new HashSet<string> { "InternalKey" },
            MaxFieldDepth = 3,
            StrictFieldValidation = true,
            FilterableFields = new HashSet<string> { "Id", "Name", "Email", "Status", "Age", "City", "Salary", "CreatedAt", "IsActive", "Description" },
            SortableFields = new HashSet<string> { "Id", "Name", "Age", "CreatedAt" },
            SelectableFields = new HashSet<string> { "Id", "Name", "Email", "Age", "City", "Status" },
        };

        _options10 = BuildOptions(10);
        _options100 = BuildOptions(100);
        _options1000 = BuildOptions(1000);
    }

    [Benchmark]
    public void Validate_10_Fields()
    {
        _options10.ValidateOrThrow<TestEntity>(_securityOptions);
    }

    [Benchmark]
    public void Validate_100_Fields()
    {
        _options100.ValidateOrThrow<TestEntity>(_securityOptions);
    }

    [Benchmark]
    public void Validate_1000_Fields()
    {
        _options1000.ValidateOrThrow<TestEntity>(_securityOptions);
    }

    private static QueryOptions BuildOptions(int fieldCount)
    {
        var fields = new List<string>(fieldCount);
        for (int i = 0; i < fieldCount; i++)
            fields.Add($"field{i}");

        var filterConditions = string.Join(",", fields.Take(10).Select(f => $"{f}:eq:value"));
        var sortFields = string.Join(",", fields.Take(5).Select(f => $"{f}:asc"));

        var parameters = new FlexQueryParameters
        {
            Filter = filterConditions,
            Sort = sortFields,
            Select = string.Join(",", fields),
            GroupBy = fields.Count > 0 ? fields[0] : null,
            Page = 1,
            PageSize = 20
        };

        return QueryOptionsParser.Parse(parameters);
    }
}
