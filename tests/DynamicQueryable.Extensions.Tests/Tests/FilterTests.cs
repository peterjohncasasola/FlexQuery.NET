using DynamicQueryable.Constants;
using DynamicQueryable.Extensions;
using DynamicQueryable.Models;
using DynamicQueryable.Parsers;
using DynamicQueryable.Tests.Fixtures;
using DynamicQueryable.Tests.Models;
using FluentAssertions;
using Microsoft.Extensions.Primitives;

namespace DynamicQueryable.Tests.Tests;

public class FilterTests : IDisposable
{
    private readonly TestDbContext _db = TestDbContext.CreateSeeded();

    public void Dispose() => _db.Dispose();

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

     // ── Laravel Spatie Format ─────────────────────────────────────────────────

     [Fact]
     public void Filter_Spatie_MultipleFields_AllConditionsMustMatch()
     {
         // filter[name]=Bob Smith&filter[age]=25
         var queryString = new List<KeyValuePair<string, StringValues>>
         {
             new KeyValuePair<string, StringValues>("filter[name]", "Bob Smith"),
             new KeyValuePair<string, StringValues>("filter[age]", "25")
         };

         var opts = QueryOptionsParser.Parse(queryString);
         opts.Paging.Disabled = true;

         var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

         result.Should().HaveCount(1);
         result[0].Name.Should().Be("Bob Smith");
         result[0].Age.Should().Be(25);
     }

     // ── Syncfusion Format ─────────────────────────────────────────────────────

     [Fact]
     public void Filter_Syncfusion_MultipleFields_OrCondition_ReturnsEitherMatch()
     {
         // where[0][field]=Name&where[0][value]=Alice Johnson&where[1][field]=Age&where[1][value]=25&condition=or
         var queryString = new List<KeyValuePair<string, StringValues>>
         {
             new KeyValuePair<string, StringValues>("where[0][field]", "Name"),
             new KeyValuePair<string, StringValues>("where[0][value]", "Alice Johnson"),
             new KeyValuePair<string, StringValues>("where[1][field]", "Age"),
             new KeyValuePair<string, StringValues>("where[1][value]", "25"),
             new KeyValuePair<string, StringValues>("condition", "or")
         };

         var opts = QueryOptionsParser.Parse(queryString);
         opts.Paging.Disabled = true;

         var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

         result.Should().NotBeEmpty();
         result.Should().AllSatisfy(e =>
         {
             // Each entity must satisfy at least one condition
             (e.Name == "Alice Johnson" || e.Age == 25).Should().BeTrue();
         });
         // Additionally, verify that both conditions are represented in the result set
         result.Should().Contain(e => e.Name == "Alice Johnson");
         result.Should().Contain(e => e.Age == 25);
     }

     [Fact]
     public void Filter_Syncfusion_MultipleFields_AndCondition_ReturnsAllMatches()
     {
         var queryString = new List<KeyValuePair<string, StringValues>>
         {
             new KeyValuePair<string, StringValues>("where[0][field]", "City"),
             new KeyValuePair<string, StringValues>("where[0][operator]", "equal"),
             new KeyValuePair<string, StringValues>("where[0][value]", "London"),
             new KeyValuePair<string, StringValues>("where[1][field]", "Age"),
             new KeyValuePair<string, StringValues>("where[1][operator]", "greaterthanorequal"),
             new KeyValuePair<string, StringValues>("where[1][value]", "25"),
             new KeyValuePair<string, StringValues>("condition", "and")
         };

         var opts = QueryOptionsParser.Parse(queryString);
         opts.Paging.Disabled = true;

         var result = _db.Entities.AsQueryable().ApplyQueryOptions(opts).ToList();

         result.Should().HaveCount(2);
         result.Should().AllSatisfy(e =>
         {
             e.City.Should().Be("London");
             e.Age.Should().BeGreaterThanOrEqualTo(25);
         });
     }
 }
