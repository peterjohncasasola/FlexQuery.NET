using FlexQuery.NET.Execution;
using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Aggregates;
using FlexQuery.NET.Options;
using FlexQuery.NET.Validation;
using FlexQuery.NET.Validation.Rules;

namespace FlexQuery.NET.Tests.Validation;

public class HavingAliasIntegrityRuleTests
{
    private sealed class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    private static QueryContext Context(Type? targetType = null, QueryGovernanceOptions? execOptions = null) =>
        new() { TargetType = targetType ?? typeof(Customer), ExecutionOptions = execOptions };

    [Fact]
    public void NullHaving_Passes()
    {
        var options = new QueryOptions { Having = null };
        var rule = new HavingAliasIntegrityRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void EmptyAggregates_Passes()
    {
        var options = new QueryOptions
        {
            Having = new HavingCondition { Function = AggregateFunction.Count, Field = "Id", Operator = "gt", Value = "5" },
            Aggregates = []
        };
        var rule = new HavingAliasIntegrityRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void MatchingAggregate_Passes()
    {
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Sum, Field = "Age", Alias = "totalAge" }],
            Having = new HavingCondition { Function = AggregateFunction.Sum, Field = "Age", Operator = "gt", Value = "100" }
        };
        var rule = new HavingAliasIntegrityRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void MismatchedAggregate_Fails()
    {
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Sum, Field = "Age", Alias = "totalAge" }],
            Having = new HavingCondition { Function = AggregateFunction.Count, Field = "Id", Operator = "gt", Value = "5" }
        };
        var rule = new HavingAliasIntegrityRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == ValidationErrorCodes.HavingAliasMismatch);
    }

    [Fact]
    public void CaseInsensitiveFieldMatch_Passes()
    {
        var options = new QueryOptions
        {
            Aggregates = [new AggregateModel { Function = AggregateFunction.Sum, Field = "Age", Alias = "totalAge" }],
            Having = new HavingCondition { Function = AggregateFunction.Sum, Field = "age", Operator = "gt", Value = "100" }
        };
        var rule = new HavingAliasIntegrityRule();
        var result = ValidationResult.Success();

        rule.Validate(options, Context(), result);

        result.IsValid.Should().BeTrue();
    }
}
