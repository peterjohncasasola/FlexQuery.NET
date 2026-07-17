using FlexQuery.NET.Caching;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Projection;
using FlexQuery.NET.Parsers;
using Microsoft.Extensions.Primitives;

namespace FlexQuery.NET.Tests.Integration;

public class QueryNormalizationTests
{
    private static QueryOptions Parse(Dictionary<string, string> raw)
    {
        var kvps = raw.ToDictionary(
            kv => kv.Key,
            kv => new StringValues(kv.Value),
            StringComparer.OrdinalIgnoreCase);
        return QueryOptionsParser.Parse(kvps);
    }

    [Fact]
    public void QueryOptionsParser_NormalizesEquivalentAndQueriesIntoCanonicalOrder()
    {
        var first = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "name", Operator = "eq", Value = "John" }, new FilterCondition { Field = "age", Operator = "gt", Value = "18" }]
            }
        };

        first = first.Normalize();

        var second = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "age", Operator = "gt", Value = "18" }, new FilterCondition { Field = "name", Operator = "eq", Value = "John" }]
            }
        }.Normalize();

        first.Filter.Should().NotBeNull();
        second.Filter.Should().NotBeNull();

        first.Filter!.Filters.Select(f => f.Field).Should().ContainInOrder("age", "name");
        second.Filter!.Filters.Select(f => f.Field).Should().ContainInOrder("age", "name");

        first.GetCacheKey(typeof(Customer), "predicate")
            .Should().Be(second.GetCacheKey(typeof(Customer), "predicate"));
    }

    [Fact]
    public void GetCacheKey_IsDeterministicForSameConditionsInDifferentOrder()
    {
        var optionsA = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters =
                [
                    new FilterCondition { Field = "Name", Operator = "eq", Value = "John" },
                    new FilterCondition { Field = "Age", Operator = "gt", Value = "18" }
                ]
            }
        };

        var optionsB = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters =
                [
                    new FilterCondition { Field = "Age", Operator = "gt", Value = "18" },
                    new FilterCondition { Field = "Name", Operator = "eq", Value = "John" }
                ]
            }
        };

        optionsA.GetCacheKey(typeof(Customer), "predicate")
            .Should().Be(optionsB.GetCacheKey(typeof(Customer), "predicate"));
    }

    [Fact]
    public void Normalize_Extension_ReturnsCanonicalFilterTree()
    {
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters =
                [
                    new FilterCondition { Field = "Name", Operator = "EQ", Value = "John" },
                    new FilterCondition { Field = "age", Operator = ">", Value = "18" }
                ]
            }
        };

        options = options.Normalize();

        options.Filter.Should().NotBeNull();
        options.Filter!.Filters.Select(f => f.Field).Should().ContainInOrder("age", "name");
        options.Filter.Filters.Select(f => f.Operator).Should().Equal(new[] { "gt", "eq" });
    }

    [Fact]
    public void ParserCache_ReturnsIsolatedQueryOptionsInstances()
    {
        ParserCache.Clear();
        var raw = new Dictionary<string, string>
        {
            ["filter"] = "Name:eq:Alice",
            ["select"] = "Id,Name"
        };

        var first = Parse(raw);
        first.Filter!.Filters[0].Field = "Mutated";
        first.Select!.Add(new SelectNode { Field = "Secret" });

        var second = Parse(raw);

        second.Filter!.Filters[0].Field.Should().Be("Name");
        second.Select.Should().BeEquivalentTo(new[] { new SelectNode { Field = "Id" }, new SelectNode { Field = "Name" } });
    }

    [Fact]
    public void GetCacheKey_DiffersForNegatedAndNonNegatedFilters()
    {
        var normal = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "Alice" }]
            }
        };

        var negated = new QueryOptions
        {
            Filter = new FilterGroup
            {
                IsNegated = true,
                Filters = [new FilterCondition { Field = "Name", Operator = "eq", Value = "Alice" }]
            }
        };

        normal.GetCacheKey(typeof(Customer), "predicate")
            .Should().NotBe(negated.GetCacheKey(typeof(Customer), "predicate"));
    }

    [Fact]
    public void GetCacheKey_DiffersForScopedFilterShape()
    {
        var statusScoped = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters =
                [
                    new FilterCondition
                    {
                        Field = "Orders",
                        Operator = "any",
                        ScopedFilter = new FilterGroup
                        {
                            Filters = [new FilterCondition { Field = "Status", Operator = "eq", Value = "Open" }]
                        }
                    }
                ]
            }
        };

        var totalScoped = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters =
                [
                    new FilterCondition
                    {
                        Field = "Orders",
                        Operator = "any",
                        ScopedFilter = new FilterGroup
                        {
                            Filters = [new FilterCondition { Field = "Total", Operator = "gt", Value = "100" }]
                        }
                    }
                ]
            }
        };

        statusScoped.GetCacheKey(typeof(Customer), "predicate")
            .Should().NotBe(totalScoped.GetCacheKey(typeof(Customer), "predicate"));
    }

    [Fact]
    public void HasProjection_ReturnsTrueForAllProjectionShapes()
    {
        new QueryOptions { Select = [new SelectNode { Field = "Id" }] }.HasProjection().Should().BeTrue();
        new QueryOptions { Includes = ["Orders"] }.HasProjection().Should().BeTrue();
        new QueryOptions { Expand = [new IncludeNode { Path = "Orders" }] }.HasProjection().Should().BeTrue();
        new QueryOptions { GroupBy = ["Status"] }.HasProjection().Should().BeTrue();
        new QueryOptions { Aggregates = [new AggregateModel { Function = AggregateFunction.Count, Alias = "Count" }] }.HasProjection().Should().BeTrue();
    }

    [Fact]
    public void Normalize_Includes_PreservesFlatPaths()
    {
        var options = new QueryOptions { Includes = ["Orders", "Details"] };

        options = options.Normalize();

        options.Includes.Should().BeEquivalentTo(["Orders", "Details"]);
        options.Expand.Should().BeNull();
    }

    [Fact]
    public void Normalize_IncludesAndExpand_KeepsBothSeparate()
    {
        var options = new QueryOptions
        {
            Includes = ["Orders", "Details"],
            Expand = [new IncludeNode { Path = "Orders", Filter = new FilterGroup { Filters = [new FilterCondition { Field = "Status", Operator = "eq", Value = "Open" }] } }]
        };

        options = options.Normalize();

        options.Includes.Should().BeEquivalentTo(["Orders", "Details"]);
        options.Expand.Should().ContainSingle(i => i.Path == "Orders" && i.Filter != null);
    }

    [Fact]
    public void Normalize_IsIdempotent()
    {
        var options = new QueryOptions
        {
            Includes = ["Orders"]
        };

        options = options.Normalize();
        var pageSizeAfterFirst = options.Paging.PageSize;

        options = options.Normalize();

        options.Paging.PageSize.Should().Be(pageSizeAfterFirst);
        options.Includes.Should().BeEquivalentTo(["Orders"]);
    }

    [Fact]
    public void Normalize_WithoutTopSkipIncludes_DoesNotMutatePaging()
    {
        var options = new QueryOptions
        {
            Paging = { Page = 3, PageSize = 25 }
        };

        options = options.Normalize();

        options.Paging.Page.Should().Be(3);
        options.Paging.PageSize.Should().Be(25);
    }
}

