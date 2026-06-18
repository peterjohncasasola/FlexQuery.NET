using FlexQuery.NET.Extensions;
using FlexQuery.NET.Caching;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Tests.Models;
using FluentAssertions;
using Microsoft.Extensions.Primitives;

namespace FlexQuery.NET.Tests.Tests;

public class QueryNormalizationTests
{
    private static QueryOptions Parse(Dictionary<string, string> raw)
    {
        var kvps = raw.Select(kv =>
            new KeyValuePair<string, StringValues>(kv.Key, new StringValues(kv.Value)));

        return QueryOptionsParser.Parse(kvps);
    }

    [Fact]
    public void QueryOptionsParser_NormalizesEquivalentAndQueriesIntoCanonicalOrder()
    {
        var first = Parse(new Dictionary<string, string>
        {
            ["query"] = "name = \"John\" AND age > 18"
        });

        var second = Parse(new Dictionary<string, string>
        {
            ["query"] = "age > 18 AND name = \"John\""
        });

        first.Filter.Should().NotBeNull();
        second.Filter.Should().NotBeNull();

        first.Filter!.Filters.Select(f => f.Field).Should().ContainInOrder("age", "name");
        second.Filter!.Filters.Select(f => f.Field).Should().ContainInOrder("age", "name");

        first.GetCacheKey(typeof(TestEntity), "predicate")
            .Should().Be(second.GetCacheKey(typeof(TestEntity), "predicate"));
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

        optionsA.GetCacheKey(typeof(TestEntity), "predicate")
            .Should().Be(optionsB.GetCacheKey(typeof(TestEntity), "predicate"));
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

        options.Normalize();

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
        first.Select!.Add("Secret");

        var second = Parse(raw);

        second.Filter!.Filters[0].Field.Should().Be("Name");
        second.Select.Should().BeEquivalentTo(["Id", "Name"]);
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

        normal.GetCacheKey(typeof(TestEntity), "predicate")
            .Should().NotBe(negated.GetCacheKey(typeof(TestEntity), "predicate"));
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

        statusScoped.GetCacheKey(typeof(TestEntity), "predicate")
            .Should().NotBe(totalScoped.GetCacheKey(typeof(TestEntity), "predicate"));
    }

    [Fact]
    public void HasProjection_ReturnsTrueForAllProjectionShapes()
    {
        new QueryOptions { Select = ["Id"] }.HasProjection().Should().BeTrue();
        new QueryOptions { Includes = ["Orders"] }.HasProjection().Should().BeTrue();
        new QueryOptions { FilteredIncludes = [new IncludeNode { Path = "Orders" }] }.HasProjection().Should().BeTrue();
        new QueryOptions { GroupBy = ["Status"] }.HasProjection().Should().BeTrue();
        new QueryOptions { Aggregates = [new AggregateModel { Function = "count", Alias = "Count" }] }.HasProjection().Should().BeTrue();

        var jsonSelect = Parse(new Dictionary<string, string>
        {
            ["filter"] = """{"select":{"id":true}}"""
        });

        jsonSelect.HasProjection().Should().BeTrue();
    }
}
