using FlexQuery.NET.Extensions;
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
}
