using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Fql;
using FlexQuery.NET.Exceptions;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Models.Projection;
using Microsoft.Extensions.Primitives;

namespace FlexQuery.NET.Tests.Tests;

public class ValidationTests
{
    private class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<Order> Orders { get; set; } = new();
    }

    private class Order
    {
        public int Id { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;
    }

    private class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void Should_Fail_When_Field_Does_Not_Exist()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "NonExistent:eq:101" } });
        var query = new List<Customer>().AsQueryable();

        Action act = () => options.ValidateOrThrow<Customer>();

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_NOT_FOUND");
    }

    [Fact]
    public void Should_Fail_When_Operator_Is_Invalid()
    {
        // Manually construct options with an invalid operator to bypass parser-level validation
        var options = new QueryOptions
        {
            Filter = new FilterGroup
            {
                Filters = new List<FilterCondition>
                {
                    new FilterCondition { Field = "Name", Operator = "invalid_op", Value = "john" }
                }
            }
        };
        var query = new List<Customer>().AsQueryable();

        Action act = () => options.ValidateOrThrow<Customer>();

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "INVALID_OPERATOR");
    }

    [Fact]
    public void Should_Fail_When_Type_Is_Incompatible()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "id:eq:not_a_number" } });
        var query = new List<Customer>().AsQueryable();

        Action act = () => options.ValidateOrThrow<Customer>();

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "TYPE_MISMATCH");
    }

    [Fact]
    public void Should_Fail_When_Nested_Field_Does_Not_Exist()
    {
        var filter = new FqlQueryParser().Parse("orders.any(Unknown = 1)");
        var options = new QueryOptions { Filter = filter };
        var query = new List<Customer>().AsQueryable();

        Action act = () => options.ValidateOrThrow<Customer>();

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "FIELD_NOT_FOUND");
    }

    [Fact]
    public void Should_Fail_When_Scoped_Filter_On_Non_Collection()
    {
        var filter = new FqlQueryParser().Parse("name.any(id = 1)");
        var options = new QueryOptions { Filter = filter };
        var query = new List<Customer>().AsQueryable();

        Action act = () => options.ValidateOrThrow<Customer>();

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == "NOT_A_COLLECTION");
    }

    [Fact]
    public void Should_Succeed_When_Query_Is_Valid()
    {
        var options = QueryOptionsParser.Parse(new Dictionary<string, StringValues> { { "filter", "name:eq:john" } });
        var query = new List<Customer>().AsQueryable();

        Action act = () => options.ValidateOrThrow<Customer>();

        act.Should().NotThrow();
    }

    [Fact]
    public void HavingWithoutGroupBy_RejectsHavingWithoutGroupByOrAggregates()
    {
        var options = new QueryOptions
        {
            Having = new HavingCondition { Function = AggregateFunction.Count, Field = null, Operator = "gt", Value = "5" }
        };

        Action act = () => options.ValidateOrThrow<Customer>();

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.HavingWithoutGroupBy);
    }

    [Fact]
    public void HavingWithoutGroupBy_AllowsHavingWithGroupBy()
    {
        var options = new QueryOptions
        {
            GroupBy = ["Status"],
            Having = new HavingCondition { Function = AggregateFunction.Count, Field = null, Operator = "gt", Value = "5" }
        };

        Action act = () => options.ValidateOrThrow<Customer>();

        act.Should().NotThrow();
    }

    [Fact]
    public void HavingWithoutGroupBy_AllowsHavingWithAggregates()
    {
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Count, Field = "Id", Alias = "idCount" }],
            Having = new HavingCondition { Function = AggregateFunction.Count, Field = "Id", Operator = "gt", Value = "5" }
        };

        Action act = () => options.ValidateOrThrow<Customer>();

        act.Should().NotThrow();
    }

    [Fact]
    public void HavingAliasIntegrity_RejectsAliasMismatch()
    {
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Sum, Field = "Total", Alias = "totalSum" }],
            Having = new HavingCondition { Function = AggregateFunction.Count, Field = "Id", Operator = "gt", Value = "5" }
        };

        Action act = () => options.ValidateOrThrow<Customer>();

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.HavingAliasMismatch);
    }

    [Fact]
    public void HavingAliasIntegrity_AllowsMatchingAlias()
    {
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Sum, Field = "Total", Alias = "totalSum" }],
            Having = new HavingCondition { Function = AggregateFunction.Sum, Field = "Total", Operator = "gt", Value = "100" }
        };

        Action act = () => options.ValidateOrThrow<Customer>();

        act.Should().NotThrow();
    }

    [Fact]
    public void GroupByIncludeConflict_RejectsGroupByWithIncludes()
    {
        var options = new QueryOptions
        {
            GroupBy = ["Status"],
            Includes = ["Orders"]
        };

        Action act = () => options.ValidateOrThrow<Customer>();

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.GroupByIncludeConflict);
    }

    [Fact]
    public void GroupByIncludeConflict_RejectsGroupByWithFilteredIncludes()
    {
        var options = new QueryOptions
        {
            GroupBy = ["Status"],
            Expand = [new IncludeNode { Path = "Orders" }]
        };

        Action act = () => options.ValidateOrThrow<Customer>();

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.GroupByIncludeConflict);
    }

    [Fact]
    public void GroupByIncludeConflict_AllowsGroupByWithoutIncludes()
    {
        var options = new QueryOptions
        {
            GroupBy = ["Status"]
        };

        Action act = () => options.ValidateOrThrow<Customer>();

        act.Should().NotThrow();
    }

    [Fact]
    public void ExpandPathValidation_RejectsInvalidIncludePath()
    {
        var options = new QueryOptions
        {
            Includes = ["NonExistentPath"]
        };

        Action act = () => options.ValidateOrThrow<Customer>();

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.IncludePathNotFound);
    }

    [Fact]
    public void ExpandPathValidation_AllowsValidIncludePath()
    {
        var options = new QueryOptions
        {
            Includes = ["Orders"]
        };

        Action act = () => options.ValidateOrThrow<Customer>();

        act.Should().NotThrow();
    }

    [Fact]
    public void ExpandPathValidation_RejectsNestedInvalidIncludePath()
    {
        var options = new QueryOptions
        {
            Expand =
            [
                new IncludeNode
                {
                    Path = "Orders",
                    Children =
                    [
                        new IncludeNode { Path = "NonExistentChild" }
                    ]
                }
            ]
        };

        Action act = () => options.ValidateOrThrow<Customer>();

        act.Should().Throw<QueryValidationException>()
           .Which.Result.Errors.Should().Contain(e => e.Code == ValidationErrorCodes.IncludePathNotFound);
    }

    [Fact]
    public void ExpandPathValidation_AllowsDeeplyNestedValidIncludePath()
    {
        var options = new QueryOptions
        {
            Expand =
            [
                new IncludeNode
                {
                    Path = "Orders",
                    Children =
                    [
                        new IncludeNode { Path = "Customer" }
                    ]
                }
            ]
        };

        Action act = () => options.ValidateOrThrow<Customer>();

        act.Should().NotThrow();
    }
}
