using FlexQuery.NET.Builders;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using FlexQuery.NET.Security;
using FlexQuery.NET.Tests.Fixtures;
using FlexQuery.NET.Tests.Models;
using FluentAssertions;

namespace FlexQuery.NET.Tests.Builders;

public class ExpressionBuilderTests : IDisposable
{
    private readonly TestDbContext _db = TestDbContext.CreateSeeded();
    public void Dispose() => _db.Dispose();

    [Fact]
    public void BuildPredicate_Eq_GeneratesCorrectExpression()
    {
        var group = new FilterGroup
        {
            Filters = [new FilterCondition { Field = "Name", Operator = FilterOperators.Equal, Value = "Alice Johnson" }]
        };
        var compiled = ExpressionBuilder.BuildPredicate<TestEntity>(group)!.Compile();
        var seed = TestDbContext.SeedData();
        compiled(seed.First(e => e.Name == "Alice Johnson")).Should().BeTrue();
        compiled(seed.First(e => e.Name == "Bob Smith")).Should().BeFalse();
    }

    [Fact]
    public void BuildPredicate_Contains_MatchesSubstring()
    {
        var group = new FilterGroup
        {
            Filters = [new FilterCondition { Field = "Name", Operator = FilterOperators.Contains, Value = "son" }]
        };
        var compiled = ExpressionBuilder.BuildPredicate<TestEntity>(group)!.Compile();
        compiled(new TestEntity { Name = "Alice Johnson" }).Should().BeTrue();
        compiled(new TestEntity { Name = "Bob Smith" }).Should().BeFalse();
    }

    [Fact]
    public void BuildPredicate_GreaterThan_FiltersNumerics()
    {
        var group = new FilterGroup
        {
            Filters = [new FilterCondition { Field = "Age", Operator = FilterOperators.GreaterThan, Value = "30" }]
        };
        var compiled = ExpressionBuilder.BuildPredicate<TestEntity>(group)!.Compile();
        compiled(new TestEntity { Age = 31 }).Should().BeTrue();
        compiled(new TestEntity { Age = 30 }).Should().BeFalse();
    }

    [Fact]
    public void BuildPredicate_LessThanOrEqual_IncludesBoundary()
    {
        var group = new FilterGroup
        {
            Filters = [new FilterCondition { Field = "Age", Operator = FilterOperators.LessThanOrEq, Value = "25" }]
        };
        var compiled = ExpressionBuilder.BuildPredicate<TestEntity>(group)!.Compile();
        compiled(new TestEntity { Age = 25 }).Should().BeTrue();
        compiled(new TestEntity { Age = 26 }).Should().BeFalse();
    }

    [Fact]
    public void BuildPredicate_NotEqual_ExcludesValue()
    {
        var group = new FilterGroup
        {
            Filters = [new FilterCondition { Field = "City", Operator = FilterOperators.NotEqual, Value = "London" }]
        };
        var compiled = ExpressionBuilder.BuildPredicate<TestEntity>(group)!.Compile();
        compiled(new TestEntity { City = "London" }).Should().BeFalse();
        compiled(new TestEntity { City = "Paris" }).Should().BeTrue();
    }

    [Fact]
    public void BuildPredicate_StartsWith_MatchesPrefix()
    {
        var group = new FilterGroup
        {
            Filters = [new FilterCondition { Field = "Name", Operator = FilterOperators.StartsWith, Value = "Alice" }]
        };
        var compiled = ExpressionBuilder.BuildPredicate<TestEntity>(group)!.Compile();
        compiled(new TestEntity { Name = "Alice Johnson" }).Should().BeTrue();
        compiled(new TestEntity { Name = "Bob Alice" }).Should().BeFalse();
    }

    [Fact]
    public void BuildPredicate_EndsWith_MatchesSuffix()
    {
        // NOTE: We no longer apply ToLower() on either side.
        // For SQL Server (case-insensitive collation), 'smith' and 'Smith' both match.
        // For in-memory / LINQ-to-Objects, string comparison is case-sensitive.
        // This test verifies the exact-case path; database collation handles case-insensitive.
        var group = new FilterGroup
        {
            Filters = [new FilterCondition { Field = "Name", Operator = FilterOperators.EndsWith, Value = "Smith" }]
        };
        var compiled = ExpressionBuilder.BuildPredicate<TestEntity>(group)!.Compile();
        compiled(new TestEntity { Name = "Bob Smith" }).Should().BeTrue();
        compiled(new TestEntity { Name = "Smith Jones" }).Should().BeFalse();
    }

    [Fact]
    public void BuildPredicate_EmptyGroup_ReturnsNull()
    {
        var predicate = ExpressionBuilder.BuildPredicate<TestEntity>(new FilterGroup());
        predicate.Should().BeNull();
    }

    [Fact]
    public void BuildPredicate_NestedProperty_ResolvesCorrectly()
    {
        var group = new FilterGroup
        {
            Filters = [new FilterCondition { Field = "Inner.Value", Operator = FilterOperators.Equal, Value = "42" }]
        };
        var compiled = ExpressionBuilder.BuildPredicate<OuterEntity>(group)!.Compile();
        compiled(new OuterEntity { Inner = new InnerEntity { Value = 42 } }).Should().BeTrue();
        compiled(new OuterEntity { Inner = new InnerEntity { Value = 99 } }).Should().BeFalse();
    }

    [Fact]
    public void BuildPredicate_InvalidNestedProperty_ReturnsNull()
    {
        var group = new FilterGroup
        {
            Filters = [new FilterCondition { Field = "Inner.DoesNotExist", Operator = FilterOperators.Equal, Value = "x" }]
        };
        var predicate = ExpressionBuilder.BuildPredicate<OuterEntity>(group);
        predicate.Should().BeNull();
    }

    [Fact]
    public void BuildPredicate_Enum_ConvertsFromString()
    {
        var group = new FilterGroup
        {
            Filters = [new FilterCondition { Field = "Status", Operator = FilterOperators.Equal, Value = "Active" }]
        };
        var compiled = ExpressionBuilder.BuildPredicate<TestEntity>(group)!.Compile();
        compiled(new TestEntity { Status = Status.Active }).Should().BeTrue();
        compiled(new TestEntity { Status = Status.Inactive }).Should().BeFalse();
    }

    [Fact]
    public void BuildPredicate_Enum_CaseInsensitiveConversion()
    {
        var group = new FilterGroup
        {
            Filters = [new FilterCondition { Field = "Status", Operator = FilterOperators.Equal, Value = "inactive" }]
        };
        var compiled = ExpressionBuilder.BuildPredicate<TestEntity>(group)!.Compile();
        compiled(new TestEntity { Status = Status.Inactive }).Should().BeTrue();
    }

    [Fact]
    public void BuildPredicate_InvalidEnumValue_ConditionIgnored()
    {
        var group = new FilterGroup
        {
            Filters = [new FilterCondition { Field = "Status", Operator = FilterOperators.Equal, Value = "NotAnEnum" }]
        };
        var predicate = ExpressionBuilder.BuildPredicate<TestEntity>(group);
        predicate.Should().BeNull();
    }

    [Fact]
    public void BuildPredicate_InvalidValueType_ReturnsNull()
    {
        var group = new FilterGroup
        {
            Filters = [new FilterCondition { Field = "Age", Operator = FilterOperators.Equal, Value = "not-an-int" }]
        };

        var predicate = ExpressionBuilder.BuildPredicate<TestEntity>(group);
        predicate.Should().BeNull();
    }

    [Fact]
    public void BuildPredicate_In_MatchesAnyValue()
    {
        var group = new FilterGroup
        {
            Filters = [new FilterCondition { Field = "Age", Operator = FilterOperators.In, Value = "25,30,35" }]
        };
        var compiled = ExpressionBuilder.BuildPredicate<TestEntity>(group)!.Compile();
        compiled(new TestEntity { Age = 25 }).Should().BeTrue();
        compiled(new TestEntity { Age = 30 }).Should().BeTrue();
        compiled(new TestEntity { Age = 27 }).Should().BeFalse();
    }

    [Fact]
    public void BuildPredicate_AndLogic_BothMustMatch()
    {
        var group = new FilterGroup
        {
            Logic = LogicOperator.And,
            Filters =
            [
                new FilterCondition { Field = "City", Operator = FilterOperators.Equal,       Value = "London" },
                new FilterCondition { Field = "Age",  Operator = FilterOperators.GreaterThan, Value = "24" }
            ]
        };
        var compiled = ExpressionBuilder.BuildPredicate<TestEntity>(group)!.Compile();
        compiled(new TestEntity { City = "London", Age = 25 }).Should().BeTrue();
        compiled(new TestEntity { City = "London", Age = 24 }).Should().BeFalse();
        compiled(new TestEntity { City = "Paris",  Age = 25 }).Should().BeFalse();
    }

    [Fact]
    public void BuildPredicate_OrLogic_EitherCanMatch()
    {
        var group = new FilterGroup
        {
            Logic = LogicOperator.Or,
            Filters =
            [
                new FilterCondition { Field = "City", Operator = FilterOperators.Equal, Value = "Berlin" },
                new FilterCondition { Field = "City", Operator = FilterOperators.Equal, Value = "Paris"  }
            ]
        };
        var compiled = ExpressionBuilder.BuildPredicate<TestEntity>(group)!.Compile();
        compiled(new TestEntity { City = "Berlin" }).Should().BeTrue();
        compiled(new TestEntity { City = "Paris"  }).Should().BeTrue();
        compiled(new TestEntity { City = "London" }).Should().BeFalse();
    }

    [Fact]
    public void ExpressionBuilder_IntegratesWithEfCoreInMemory()
    {
        var group = new FilterGroup
        {
            Logic = LogicOperator.And,
            Filters =
            [
                new FilterCondition { Field = "City", Operator = FilterOperators.Equal,    Value = "New York" },
                new FilterCondition { Field = "Age",  Operator = FilterOperators.LessThan, Value = "40"       }
            ]
        };
        var predicate = ExpressionBuilder.BuildPredicate<TestEntity>(group)!;
        var result = _db.Entities.AsQueryable().Where(predicate).ToList();
        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(e =>
        {
            e.City.Should().Be("New York");
            e.Age.Should().BeLessThan(40);
        });
    }

    [Fact]
    public void BuildPredicate_CollectionNestedPath_UsesAnyTraversal()
    {
        var group = new FilterGroup
        {
            Filters = [new FilterCondition { Field = "Orders.Customer.Name", Operator = FilterOperators.Equal, Value = "Alice" }]
        };

        var compiled = ExpressionBuilder.BuildPredicate<CollectionRoot>(group)!.Compile();
        compiled(new CollectionRoot
        {
            Orders = [new CollectionOrder { Customer = new CollectionCustomer { Name = "Alice" } }]
        }).Should().BeTrue();
        compiled(new CollectionRoot
        {
            Orders = [new CollectionOrder { Customer = new CollectionCustomer { Name = "Bob" } }]
        }).Should().BeFalse();
    }

    [Fact]
    public void BuildPredicate_FieldWhitelist_RejectsUnknownField()
    {
        FieldRegistry.Register<TestEntity>(["Name", "Age"]);
        try
        {
            var group = new FilterGroup
            {
                Filters = [new FilterCondition { Field = "City", Operator = FilterOperators.Equal, Value = "London" }]
            };

            var predicate = ExpressionBuilder.BuildPredicate<TestEntity>(group);
            predicate.Should().BeNull();
        }
        finally
        {
            FieldRegistry.Clear<TestEntity>();
        }
    }
}

public class OuterEntity
{
    public int         Id    { get; set; }
    public InnerEntity Inner { get; set; } = new();
}

public class InnerEntity
{
    public int Value { get; set; }
}

public class CollectionRoot
{
    public List<CollectionOrder> Orders { get; set; } = [];
}

public class CollectionOrder
{
    public CollectionCustomer Customer { get; set; } = new();
}

public class CollectionCustomer
{
    public string Name { get; set; } = string.Empty;
}
