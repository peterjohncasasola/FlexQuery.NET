using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Parsers;
using Microsoft.Extensions.Primitives;

namespace FlexQuery.NET.Tests.Parsers;

public class DslQueryParserTests
{
    private static QueryOptions Parse(Dictionary<string, string> raw)
    {
        var kvps = raw.Select(kv =>
            new KeyValuePair<string, StringValues>(kv.Key, new StringValues(kv.Value)));
        return QueryOptionsParser.Parse(kvps);
    }

    [Fact]
    public void DslFilter_GroupedOrAndCondition_ParsedToNestedFilterGroup()
    {
        var opts = Parse(new()
        {
            ["filter"] = "(name:eq:john|name:eq:doe)&age:gt:20"
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Logic.Should().Be(LogicOperator.And);
        opts.Filter.Filters.Should().ContainSingle(f =>
            f.Field == "age" && f.Operator == FilterOperators.GreaterThan && f.Value == "20");
        opts.Filter.Groups.Should().HaveCount(1);
        opts.Filter.Groups[0].Logic.Should().Be(LogicOperator.Or);
        opts.Filter.Groups[0].Filters.Should().HaveCount(2);
        opts.Filter.Groups[0].Filters.Should().AllSatisfy(f => f.Operator.Should().Be(FilterOperators.Equal));
    }

    [Fact]
    public void DslFilter_NestedPathAndInOperator_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["filter"] = "orders.customer.name:contains:'john doe'&status:in:Active,Pending",
            ["page"] = "2",
            ["pageSize"] = "15"
        });

        opts.Filter!.Logic.Should().Be(LogicOperator.And);
        opts.Filter.Filters.Should().HaveCount(2);
        opts.Filter.Filters[0].Field.Should().Be("orders.customer.name");
        opts.Filter.Filters[0].Operator.Should().Be(FilterOperators.Contains);
        opts.Filter.Filters[0].Value.Should().Be("john doe");
        opts.Filter.Filters[1].Operator.Should().Be(FilterOperators.In);
        opts.Filter.Filters[1].Value.Should().Be("Active,Pending");
        opts.Paging.Page.Should().Be(2);
        opts.Paging.PageSize.Should().Be(15);
    }

    [Fact]
    public void DslFilter_IsNull_DoesNotRequireValue()
    {
        var opts = Parse(new()
        {
            ["filter"] = "deletedAt:isnull"
        });

        opts.Filter!.Filters.Should().ContainSingle();
        opts.Filter.Filters[0].Field.Should().Be("deletedAt");
        opts.Filter.Filters[0].Operator.Should().Be(FilterOperators.IsNull);
        opts.Filter.Filters[0].Value.Should().BeNull();
    }

    [Fact]
    public void DslFilter_MalformedDsl_IsIgnoredGracefully()
    {
        var opts = Parse(new()
        {
            ["filter"] = "name:eq:"
        });

        opts.Filter.Should().BeNull();
    }

    [Fact]
    public void DslFilter_PhaseTwoAndThreeOperators_AreParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["filter"] = "(name:startswith:jo|name:endswith:hn)&status:notin:Inactive,Deleted&age:between:18,60&deletedAt:notnull"
        });

        opts.Filter!.Logic.Should().Be(LogicOperator.And);
        opts.Filter.Groups.Should().ContainSingle();
        opts.Filter.Groups[0].Logic.Should().Be(LogicOperator.Or);
        opts.Filter.Groups[0].Filters.Select(f => f.Operator)
            .Should().BeEquivalentTo([FilterOperators.StartsWith, FilterOperators.EndsWith]);
        opts.Filter.Filters.Select(f => f.Operator).Should().Contain([
            FilterOperators.NotIn,
            FilterOperators.Between,
            FilterOperators.IsNotNull
        ]);
    }

    [Fact]
    public void DslFilter_NestedDynamicConditions_ParsedToRecursiveGroups()
    {
        var opts = Parse(new()
        {
            ["filter"] = "((city:eq:London|city:eq:Berlin)&(age:between:25,40|status:eq:Pending))"
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Logic.Should().Be(LogicOperator.And);
        opts.Filter.Filters.Should().BeEmpty();
        opts.Filter.Groups.Should().HaveCount(2);

        opts.Filter.Groups[0].Logic.Should().Be(LogicOperator.Or);
        opts.Filter.Groups[0].Filters.Should().HaveCount(2);
        opts.Filter.Groups[0].Filters.Select(f => f.Field).Should().BeEquivalentTo(["city", "city"]);
        opts.Filter.Groups[0].Filters.Select(f => f.Operator).Should().AllSatisfy(op => op.Should().Be(FilterOperators.Equal));

        opts.Filter.Groups[1].Logic.Should().Be(LogicOperator.Or);
        opts.Filter.Groups[1].Filters.Should().HaveCount(2);
        opts.Filter.Groups[1].Filters.Should().Contain(f =>
            f.Field == "age" && f.Operator == FilterOperators.Between && f.Value == "25,40");
        opts.Filter.Groups[1].Filters.Should().Contain(f =>
            f.Field == "status" && f.Operator == FilterOperators.Equal && f.Value == "Pending");
    }

    [Fact]
    public void Dsl_ValidSimple_ParsesIntoFilterGroup()
    {
        var opts = Parse(new()
        {
            ["filter"] = "(name:eq:john|name:eq:doe)&age:gt:20"
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Logic.Should().Be(LogicOperator.And);
        opts.Filter.Filters.Should().ContainSingle(f => f.Field == "age" && f.Operator == FilterOperators.GreaterThan);
        opts.Filter.Groups.Should().ContainSingle();
        opts.Filter.Groups[0].Logic.Should().Be(LogicOperator.Or);
    }

    [Fact]
    public void Dsl_InvalidOperator_IsRejected()
    {
        var opts = Parse(new()
        {
            ["filter"] = "age:unknown:20"
        });

        opts.Filter.Should().BeNull();
    }

    [Fact]
    public void Dsl_MalformedExpression_IsRejected()
    {
        var opts = Parse(new()
        {
            ["filter"] = "(name:eq:john|age:gt:20"
        });

        opts.Filter.Should().BeNull();
    }

    [Fact]
    public void Dsl_Value_AllowsEmail()
    {
        var opts = Parse(new()
        {
            ["filter"] = "email:eq:ops@acmeretail.com"
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Filters.Should().ContainSingle(f => f.Field == "email" && f.Value == "ops@acmeretail.com");
    }

    [Fact]
    public void Dsl_Value_AllowsSpaces()
    {
        var opts = Parse(new()
        {
            ["filter"] = "name:contains:john doe"
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Filters.Should().ContainSingle(f => f.Field == "name" && f.Value == "john doe");
    }

    [Fact]
    public void Dsl_Value_AllowsUrls()
    {
        var opts = Parse(new()
        {
            ["filter"] = "url:eq:https://example.com/page"
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Filters.Should().ContainSingle(f => f.Field == "url" && f.Value == "https://example.com/page");
    }

    [Fact]
    public void Dsl_Value_AllowsAdditionalColons()
    {
        var opts = Parse(new()
        {
            ["filter"] = "note:eq:value:with:colon"
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Filters.Should().ContainSingle(f => f.Field == "note" && f.Value == "value:with:colon");
    }

    [Fact]
    public void Dsl_Value_SupportsQuotedValues()
    {
        var opts = Parse(new()
        {
            ["filter"] = "email:eq:\"ops@acmeretail.com\""
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Filters.Should().ContainSingle(f => f.Field == "email" && f.Value == "ops@acmeretail.com");
    }

    [Fact]
    public void Dsl_NotPrefix_ParsesAsNegatedConditionGroup()
    {
        var opts = Parse(new()
        {
            ["filter"] = "!name:eq:john"
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.IsNegated.Should().BeTrue();
        opts.Filter.Filters.Should().ContainSingle(f =>
            f.Field == "name"
            && f.Operator == FilterOperators.Equal
            && f.Value == "john");
    }

    [Fact]
    public void Dsl_NotFunction_ParsesAsNegatedConditionGroup()
    {
        var opts = Parse(new()
        {
            ["filter"] = "not(name:eq:john)"
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.IsNegated.Should().BeTrue();
        opts.Filter.Filters.Should().ContainSingle(f =>
            f.Field == "name"
            && f.Operator == FilterOperators.Equal
            && f.Value == "john");
    }

    [Fact]
    public void Dsl_AnyOperator_ParsesWithRawInnerExpression()
    {
        var opts = Parse(new()
        {
            ["filter"] = "orders:any:total:gt:100"
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Filters.Should().ContainSingle(f =>
            f.Field == "orders"
            && f.Operator == FilterOperators.Any
            && f.Value == "total:gt:100");
    }

    [Fact]
    public void Dsl_CountOperator_ParsesWithRawComparisonExpression()
    {
        var opts = Parse(new()
        {
            ["filter"] = "orders:count:gt:5"
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Filters.Should().ContainSingle(f =>
            f.Field == "orders"
            && f.Operator == FilterOperators.Count
            && f.Value == "gt:5");
    }

    // ════════════════════════════════════════════════════════════════════
    // Sort
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void Sort_SingleField_Ascending()
    {
        var opts = Parse(new()
        {
            ["sort"] = "name:asc"
        });

        opts.Sort.Should().HaveCount(1);
        opts.Sort[0].Field.Should().Be("name");
        opts.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Sort_SingleField_Descending()
    {
        var opts = Parse(new()
        {
            ["sort"] = "name:desc"
        });

        opts.Sort.Should().HaveCount(1);
        opts.Sort[0].Field.Should().Be("name");
        opts.Sort[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void Sort_SingleField_NoDirection_DefaultsToAscending()
    {
        var opts = Parse(new()
        {
            ["sort"] = "name"
        });

        opts.Sort.Should().HaveCount(1);
        opts.Sort[0].Field.Should().Be("name");
        opts.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Sort_MultiField_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["sort"] = "createdAt:desc,total:asc"
        });

        opts.Sort.Should().HaveCount(2);
        opts.Sort[0].Field.Should().Be("createdAt");
        opts.Sort[0].Descending.Should().BeTrue();
        opts.Sort[1].Field.Should().Be("total");
        opts.Sort[1].Descending.Should().BeFalse();
    }

    [Fact]
    public void Sort_NestedFields_DotNotation()
    {
        var opts = Parse(new()
        {
            ["sort"] = "profile.bio:asc,name:desc"
        });

        opts.Sort.Should().HaveCount(2);
        opts.Sort[0].Field.Should().Be("profile.bio");
        opts.Sort[0].Descending.Should().BeFalse();
        opts.Sort[1].Field.Should().Be("name");
        opts.Sort[1].Descending.Should().BeTrue();
    }

    [Fact]
    public void Sort_InvalidDirection_DefaultsToAscending()
    {
        var opts = Parse(new()
        {
            ["sort"] = "name:sideways"
        });

        opts.Sort.Should().ContainSingle();
        opts.Sort[0].Field.Should().Be("name");
        opts.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Sort_Aggregate_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["sort"] = "orders.sum(total):desc,orders.count():asc"
        });

        opts.Sort.Should().HaveCount(2);

        opts.Sort[0].Field.Should().Be("orders");
        opts.Sort[0].Aggregate.Should().Be("sum");
        opts.Sort[0].AggregateField.Should().Be("total");
        opts.Sort[0].Descending.Should().BeTrue();

        opts.Sort[1].Field.Should().Be("orders");
        opts.Sort[1].Aggregate.Should().Be("count");
        opts.Sort[1].AggregateField.Should().BeNull();
        opts.Sort[1].Descending.Should().BeFalse();
    }

    [Fact]
    public void Sort_EmptyString_ReturnsEmptyList()
    {
        var opts = Parse(new()
        {
            ["sort"] = ""
        });

        opts.Sort.Should().BeEmpty();
    }

    // ════════════════════════════════════════════════════════════════════
    // Pagination
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void Paging_PageAndPageSize_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["page"] = "3",
            ["pageSize"] = "25"
        });

        opts.Paging.Page.Should().Be(3);
        opts.Paging.PageSize.Should().Be(25);
    }

    [Fact]
    public void Paging_DefaultValues()
    {
        var opts = Parse(new());

        opts.Paging.Page.Should().Be(1);
        opts.Paging.PageSize.Should().Be(20);
        opts.Paging.Disabled.Should().BeFalse();
    }

    // ════════════════════════════════════════════════════════════════════
    // Projection
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void Select_CommaSeparatedFields_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["select"] = "Id,Name,Email"
        });

        opts.Select.Should().BeEquivalentTo(["Id", "Name", "Email"]);
    }

    [Fact]
    public void Select_EmptyString_ReturnsNull()
    {
        var opts = Parse(new()
        {
            ["select"] = ""
        });

        opts.Select.Should().BeNull();
    }

    [Fact]
    public void Select_SingleField_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["select"] = "Name"
        });

        opts.Select.Should().BeEquivalentTo(["Name"]);
    }

    // ════════════════════════════════════════════════════════════════════
    // Grouping / Aggregates / Having
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void GroupAndAggregateSelect_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["group"] = "category,status",
            ["select"] = "category,sum(total),count(id)",
            ["having"] = "sum(total):gt:10000"
        });

        opts.GroupBy.Should().BeEquivalentTo(["category", "status"]);
        opts.Select.Should().BeEquivalentTo(["category"]);
        opts.Aggregates.Should().HaveCount(2);
        opts.Aggregates.Should().Contain(a => a.Function == "sum" && a.Field == "total");
        opts.Aggregates.Should().Contain(a => a.Function == "count" && a.Field == "id");
        opts.Having.Should().NotBeNull();
        opts.Having!.Function.Should().Be("sum");
        opts.Having.Operator.Should().Be(FilterOperators.GreaterThan);
        opts.Having.Value.Should().Be("10000");
    }

    [Fact]
    public void GroupBy_SingleField_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["group"] = "category"
        });

        opts.GroupBy.Should().BeEquivalentTo(["category"]);
    }

    [Fact]
    public void GroupBy_MultipleFields_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["group"] = "category,status,region"
        });

        opts.GroupBy.Should().BeEquivalentTo(["category", "status", "region"]);
    }

    [Fact]
    public void Having_Count_GreaterThan()
    {
        var opts = Parse(new()
        {
            ["group"] = "status",
            ["select"] = "status,count()",
            ["having"] = "count:gt:20"
        });

        opts.Having.Should().NotBeNull();
        opts.Having!.Function.Should().Be("count");
        opts.Having.Field.Should().BeNull();
        opts.Having.Operator.Should().Be(FilterOperators.GreaterThan);
        opts.Having.Value.Should().Be("20");
    }

    [Fact]
    public void Having_Sum_WithField()
    {
        var opts = Parse(new()
        {
            ["group"] = "status",
            ["select"] = "status,sum(total)",
            ["having"] = "sum:total:gt:100"
        });

        opts.Having.Should().NotBeNull();
        opts.Having!.Function.Should().Be("sum");
        opts.Having.Field.Should().Be("total");
        opts.Having.Operator.Should().Be(FilterOperators.GreaterThan);
        opts.Having.Value.Should().Be("100");
    }

    [Fact]
    public void Having_ParenthesisFormat_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["group"] = "status",
            ["select"] = "status,sum(total)",
            ["having"] = "sum(total):gt:100"
        });

        opts.Having.Should().NotBeNull();
        opts.Having!.Function.Should().Be("sum");
        opts.Having.Field.Should().Be("total");
        opts.Having.Operator.Should().Be(FilterOperators.GreaterThan);
        opts.Having.Value.Should().Be("100");
    }

    // ════════════════════════════════════════════════════════════════════
    // Empty / Default Input
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void EmptyInput_ReturnsDefaultOptions()
    {
        var opts = Parse(new());

        opts.Filter.Should().BeNull();
        opts.Sort.Should().BeEmpty();
        opts.Select.Should().BeNull();
        opts.Paging.Page.Should().Be(1);
        opts.Paging.PageSize.Should().Be(20);
    }
}
