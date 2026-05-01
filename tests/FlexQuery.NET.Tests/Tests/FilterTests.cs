using FlexQuery.NET.Constants;
using FlexQuery.NET;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Tests.Fixtures;
using FlexQuery.NET.Tests.Models;
using FluentAssertions;
using Microsoft.Extensions.Primitives;

namespace FlexQuery.NET.Tests.Tests;

public class FilterTests : IDisposable
{
    private readonly TestDbContext _db = TestDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

    private static QueryOptions Parse(Dictionary<string, string> raw)
    {
        var kvps = raw.Select(kv =>
            new KeyValuePair<string, StringValues>(kv.Key, new StringValues(kv.Value)));
        return QueryOptionsParser.Parse(kvps);
    }

    // ── Equality ─────────────────────────────────────────────────────────

    [Fact]
    public void Filter_Eq_ByName_ReturnsExactMatch()
    {
        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = FilterOperators.Equal, Value = "Alice Johnson" }]
            }
        };

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Alice Johnson");
    }

    [Fact]
    public void Filter_Eq_ByAge_ReturnsCorrectEntity()
    {
        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Age", Operator = FilterOperators.Equal, Value = "25" }]
            },
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Bob Smith");
    }

    // ── Not Equal ────────────────────────────────────────────────────────

    [Fact]
    public void Filter_Neq_ExcludesMatchingEntity()
    {
        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "City", Operator = FilterOperators.NotEqual, Value = "London" }]
            },
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().NotContain(e => e.City == "London");
        result.Should().HaveCount(7); // 10 total, 3 in London
    }

    // ── Contains ─────────────────────────────────────────────────────────

    [Fact]
    public void Filter_Contains_CaseInsensitive_ReturnsMatches()
    {
        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = FilterOperators.Contains, Value = "son" }]
            },
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        // "Alice Johnson" and "Jack Anderson" contain "son"
        result.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Should().AllSatisfy(e => e.Name.Contains("son", StringComparison.OrdinalIgnoreCase));
    }

    // ── Greater Than / Less Than ─────────────────────────────────────────

    [Fact]
    public void Filter_StartsWith_ReturnsPrefixMatches()
    {
        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = FilterOperators.StartsWith, Value = "Al" }]
            },
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().ContainSingle();
        result[0].Name.Should().Be("Alice Johnson");
    }

    [Fact]
    public void Filter_EndsWith_ReturnsSuffixMatches()
    {
        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Name", Operator = FilterOperators.EndsWith, Value = "son" }]
            },
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().HaveCount(3);
        result.Should().AllSatisfy(e => e.Name.EndsWith("son", StringComparison.OrdinalIgnoreCase).Should().BeTrue());
    }

    [Fact]
    public void Filter_GreaterThan_Age_ReturnsOlderEntities()
    {
        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Age", Operator = FilterOperators.GreaterThan, Value = "35" }]
            },
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(e => e.Age.Should().BeGreaterThan(35));
    }

    [Fact]
    public void Filter_LessThan_Age_ReturnsYoungerEntities()
    {
        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Age", Operator = FilterOperators.LessThan, Value = "25" }]
            },
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(e => e.Age.Should().BeLessThan(25));
    }

    [Fact]
    public void Filter_GreaterThanOrEqual_Age_IncludesBoundary()
    {
        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Age", Operator = FilterOperators.GreaterThanOrEq, Value = "40" }]
            },
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().AllSatisfy(e => e.Age.Should().BeGreaterThanOrEqualTo(40));
        result.Any(e => e.Age == 40).Should().BeTrue();
    }

    // ── Nested AND/OR ────────────────────────────────────────────────────

    [Fact]
    public void Filter_Between_Age_ReturnsInclusiveRange()
    {
        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Age", Operator = FilterOperators.Between, Value = "25,35" }]
            },
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(e => e.Age.Should().BeInRange(25, 35));
        result.Should().Contain(e => e.Age == 25);
        result.Should().Contain(e => e.Age == 35);
    }

    [Fact]
    public void Filter_Between_CreatedAt_ReturnsInclusiveDateRange()
    {
        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "CreatedAt", Operator = FilterOperators.Between, Value = "2023-03-01,2023-05-01" }]
            },
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().HaveCount(3);
        result.Should().AllSatisfy(e =>
            e.CreatedAt.Should().BeOnOrAfter(new DateTime(2023, 3, 1)).And.BeOnOrBefore(new DateTime(2023, 5, 1)));
    }

    [Fact]
    public void Filter_NotIn_ExcludesConvertedValues()
    {
        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Status", Operator = FilterOperators.NotIn, Value = "Inactive,Pending" }]
            },
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(e => e.Status.Should().Be(Status.Active));
    }

    [Fact]
    public void Filter_NestedAnd_BothConditionsMustMatch()
    {
        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Logic = LogicOperator.And,
                Filters =
                [
                    new FilterCondition { Field = "City",   Operator = FilterOperators.Equal, Value = "New York" },
                    new FilterCondition { Field = "Age",    Operator = FilterOperators.GreaterThan, Value = "29" }
                ]
            },
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(e =>
        {
            e.City.Should().Be("New York");
            e.Age.Should().BeGreaterThan(29);
        });
    }

    [Fact]
    public void Filter_NestedOr_EitherConditionCanMatch()
    {
        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Logic = LogicOperator.Or,
                Filters =
                [
                    new FilterCondition { Field = "City", Operator = FilterOperators.Equal, Value = "Berlin" },
                    new FilterCondition { Field = "City", Operator = FilterOperators.Equal, Value = "Paris"  }
                ]
            },
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(e => e.City.Should().BeOneOf("Berlin", "Paris"));
    }

    [Fact]
    public void Filter_NestedGroups_ComplexAndOrTree()
    {
        // (City = "New York" OR City = "London") AND Age > 25
        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Logic = LogicOperator.And,
                Filters = [new FilterCondition { Field = "Age", Operator = FilterOperators.GreaterThan, Value = "25" }],
                Groups =
                [
                    new FilterGroup
                    {
                        Logic = LogicOperator.Or,
                        Filters =
                        [
                            new FilterCondition { Field = "City", Operator = FilterOperators.Equal, Value = "New York" },
                            new FilterCondition { Field = "City", Operator = FilterOperators.Equal, Value = "London"   }
                        ]
                    }
                ]
            },
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(e =>
        {
            e.City.Should().BeOneOf("New York", "London");
            e.Age.Should().BeGreaterThan(25);
        });
    }

    // ── Null handling ────────────────────────────────────────────────────

    [Fact]
    public void Filter_DslNestedDynamicConditions_AppliesRecursiveGroups()
    {
        var opts = Parse(new()
        {
            ["filter"] = "((city:eq:London|city:eq:Berlin)&(age:between:25,40|status:eq:Pending))"
        });
        opts.Paging.Disabled = true;

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().HaveCount(4);
        result.Should().AllSatisfy(e =>
        {
            e.City.Should().BeOneOf("London", "Berlin");
            (e.Age is >= 25 and <= 40 || e.Status == Status.Pending).Should().BeTrue();
        });
        result.Select(e => e.Name).Should().BeEquivalentTo(
            ["Bob Smith", "Frank Miller", "Ivy Taylor", "Jack Anderson"]);
    }

    [Fact]
    public void Filter_DslNotPrefix_NegatesCondition()
    {
        var opts = Parse(new()
        {
            ["filter"] = "!name:eq:Alice Johnson"
        });
        opts.Paging.Disabled = true;

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().HaveCount(9);
        result.Should().NotContain(e => e.Name == "Alice Johnson");
    }

    [Fact]
    public void Filter_DslLike_UsesSqlPatternSemantics()
    {
        var opts = Parse(new()
        {
            ["filter"] = "name:like:%son%"
        });
        opts.Paging.Disabled = true;

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().HaveCount(3);
        result.Select(e => e.Name).Should().BeEquivalentTo(["Alice Johnson", "Grace Wilson", "Jack Anderson"]);
    }

    [Fact]
    public void Filter_DslAny_FiltersByCollectionElementPredicate()
    {
        var opts = Parse(new()
        {
            ["filter"] = "orders:any:total:gt:100"
        });
        opts.Paging.Disabled = true;

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().ContainSingle();
        result[0].Name.Should().Be("David Brown");
    }

    [Fact]
    public void Filter_DslCount_FiltersByCollectionCount()
    {
        var opts = Parse(new()
        {
            ["filter"] = "orders:count:gt:1"
        });
        opts.Paging.Disabled = true;

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().ContainSingle();
        result[0].Name.Should().Be("Alice Johnson");
    }

    [Fact]
    public void Filter_IsNull_OnNonNullableField_ReturnsNothing()
    {
        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Age", Operator = FilterOperators.IsNull }]
            },
            Paging = { Disabled = true }
        };

        // Age is a non-nullable int — isNull always returns false
        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Filter_IsNotNull_OnNonNullableField_ReturnsAll()
    {
        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Age", Operator = FilterOperators.IsNotNull }]
            },
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();
        result.Should().HaveCount(10);
    }

    [Fact]
    public void Filter_NotNull_Alias_ReturnsNonNullReferenceValues()
    {
        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "Profile", Operator = "notnull" }]
            },
            Paging = { Disabled = true }
        };

        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

        result.Should().HaveCount(3);
        result.Should().AllSatisfy(e => e.Profile.Should().NotBeNull());
    }

    [Fact]
    public void Filter_InvalidField_IsIgnoredGracefully()
    {
        var opts = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "NonExistentProperty", Operator = "eq", Value = "x" }]
            },
            Paging = { Disabled = true }
        };

        // Should not throw — invalid field produces null expression → no filter applied
        var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();
        result.Should().HaveCount(10);
    }

    [Fact]
    public void Filter_EmptyGroup_ReturnsAllRecords()
    {
        var opts = new QueryOptions
        {
            Filter  = new FilterGroup(), // empty
            Paging  = { Disabled = true }
        };

         var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();
         result.Should().HaveCount(10);
     }


 }
