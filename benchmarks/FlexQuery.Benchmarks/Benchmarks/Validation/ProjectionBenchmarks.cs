using BenchmarkDotNet.Attributes;
using FlexQuery.NET;
using FlexQuery.NET.Helpers;
using FlexQuery.NET.Builders;
using FlexQuery.NET.Caching;
using FlexQuery.NET.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FlexQuery.Benchmarks.Benchmarks.Validation;

[Config(typeof(Infrastructure.CustomBenchmarkConfig))]
public class ProjectionBenchmarks
{
    private class FlatEntity
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

    private class DeepEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public NavA NavA { get; set; } = new();
    }

    private class NavA { public int Id { get; set; } public string Value { get; set; } = string.Empty; public NavB NavB { get; set; } = new(); }
    private class NavB { public int Id { get; set; } public string Value { get; set; } = string.Empty; public NavC NavC { get; set; } = new(); }
    private class NavC { public int Id { get; set; } public string Value { get; set; } = string.Empty; public NavD NavD { get; set; } = new(); }
    private class NavD { public int Id { get; set; } public string Value { get; set; } = string.Empty; }

    private QueryOptions _flatOptions10 = null!;
    private QueryOptions _flatOptions100 = null!;
    private QueryOptions _flatOptions1000 = null!;
    private QueryOptions _nestedOptions = null!;
    private QueryOptions _wildcardOptions = null!;
    private QueryOptions _cacheKeyOptionsRealistic = null!;
    private QueryOptions _cacheKeyOptionsTree100 = null!;
    private int _dynamicTypeCounter;

    [GlobalSetup]
    public void Setup()
    {
        _flatOptions10 = new QueryOptions { Select = BuildFlatSelectList(10) };
        _flatOptions100 = new QueryOptions { Select = BuildFlatSelectList(100) };
        _flatOptions1000 = new QueryOptions { Select = BuildFlatSelectList(1000) };
        _nestedOptions = new QueryOptions { SelectTree = BuildNestedTree() };

        var wildcardTree = new SelectionNode();
        wildcardTree.MarkIncludeAllScalars();
        _wildcardOptions = new QueryOptions { SelectTree = wildcardTree };

        _cacheKeyOptionsTree100 = new QueryOptions
        {
            SelectTree = BuildFlatTree(100),
            Select = new List<string> { "Id", "Name", "Email" },
        };

        _cacheKeyOptionsRealistic = new QueryOptions
        {
            SelectTree = BuildFlatTree(10),
            Select = new List<string>
            {
                "Id", "Name", "Email", "Status", "Age", "City", "Salary", "CreatedAt", "IsActive", "Description",
            },
            Sort = new List<SortNode>
            {
                new SortNode { Field = "Name", Descending = false },
                new SortNode { Field = "CreatedAt", Descending = true },
            },
            Filter = new FilterGroup
            {
                Filters = new List<FilterCondition>
                {
                    new FilterCondition { Field = "Status", Operator = "eq", Value = "active" },
                    new FilterCondition { Field = "Age", Operator = "gte", Value = "18" },
                },
                Logic = LogicOperator.And,
            },
            Paging = new PagingOptions { Page = 1, PageSize = 50 },
            CaseInsensitive = true,
        };
    }

    [Benchmark]
    public SelectionNode SelectTreeBuilder_Flat_10()
    {
        return SelectTreeBuilder.Build(_flatOptions10);
    }

    [Benchmark]
    public SelectionNode SelectTreeBuilder_Flat_100()
    {
        return SelectTreeBuilder.Build(_flatOptions100);
    }

    [Benchmark]
    public SelectionNode SelectTreeBuilder_Flat_1000()
    {
        return SelectTreeBuilder.Build(_flatOptions1000);
    }

    [Benchmark]
    public SelectionNode SelectTreeBuilder_Nested_Deep()
    {
        return SelectTreeBuilder.Build(_nestedOptions);
    }

    [Benchmark]
    public SelectionNode SelectTreeBuilder_Wildcard_AllScalars()
    {
        return SelectTreeBuilder.Build(_wildcardOptions);
    }

    [Benchmark]
    public Type DynamicType_CacheHit()
    {
        var props = new Dictionary<string, Type>
        {
            ["Id"] = typeof(int), ["Name"] = typeof(string), ["Email"] = typeof(string),
            ["Status"] = typeof(string), ["Age"] = typeof(int), ["City"] = typeof(string),
            ["Salary"] = typeof(decimal), ["CreatedAt"] = typeof(DateTime),
            ["IsActive"] = typeof(bool), ["Description"] = typeof(string),
        };
        return DynamicTypeBuilder.GetDynamicType(props);
    }

    [Benchmark]
    public Type DynamicType_Empty()
    {
        return DynamicTypeBuilder.GetDynamicType(new Dictionary<string, Type>());
    }

    [Benchmark]
    public Type DynamicType_CacheMiss()
    {
        var props = new Dictionary<string, Type>
        {
            [$"Field{Interlocked.Increment(ref _dynamicTypeCounter)}"] = typeof(int)
        };
        return DynamicTypeBuilder.GetDynamicType(props);
    }

    [Benchmark]
    public long Projection_BuildCacheKey()
    {
        return QueryCacheKeyBuilder.Build(_cacheKeyOptionsRealistic, typeof(FlatEntity), "query").Length;
    }

    [Benchmark]
    public long Projection_BuildCacheKey_Tree100()
    {
        return QueryCacheKeyBuilder.Build(_cacheKeyOptionsTree100, typeof(FlatEntity), "query").Length;
    }

    private static List<string> BuildFlatSelectList(int count)
    {
        var list = new List<string>(count);
        for (int i = 0; i < count; i++)
            list.Add($"field{i}");
        return list;
    }

    private static SelectionNode BuildFlatTree(int count)
    {
        var root = new SelectionNode();
        for (int i = 0; i < count; i++)
            root.GetOrAddChild($"field{i}");
        return root;
    }

    private static SelectionNode BuildNestedTree()
    {
        var root = new SelectionNode();
        root.GetOrAddChild("Id");
        root.GetOrAddChild("Name");
        var navA = root.GetOrAddChild("NavA");
        navA.GetOrAddChild("Id");
        navA.GetOrAddChild("Value");
        var navB = navA.GetOrAddChild("NavB");
        navB.GetOrAddChild("Id");
        navB.GetOrAddChild("Value");
        var navC = navB.GetOrAddChild("NavC");
        navC.GetOrAddChild("Id");
        navC.GetOrAddChild("Value");
        navC.GetOrAddChild("NavD").GetOrAddChild("Id");
        return root;
    }
}
