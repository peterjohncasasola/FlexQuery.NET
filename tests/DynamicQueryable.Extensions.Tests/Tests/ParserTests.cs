using DynamicQueryable.Constants;
using DynamicQueryable.Models;
using DynamicQueryable.Parsers;
using DynamicQueryable.Parsers.Jql;
using FluentAssertions;
using Microsoft.Extensions.Primitives;

namespace DynamicQueryable.Tests.Tests;

/// <summary>
/// Tests for QueryOptionsParser covering all 4 query-string formats.
/// </summary>
public class ParserTests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    private static QueryOptions Parse(Dictionary<string, string> raw)
    {
        var kvps = raw.Select(kv =>
            new KeyValuePair<string, StringValues>(kv.Key, new StringValues(kv.Value)));
        return QueryOptionsParser.Parse(kvps);
    }

    // ════════════════════════════════════════════════════════════════════
    // 1. Generic Format
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void Generic_SingleFilter_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["filter[0].field"]    = "Name",
            ["filter[0].operator"] = "contains",
            ["filter[0].value"]    = "john",
            ["page"]               = "1",
            ["pageSize"]           = "10"
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Filters.Should().HaveCount(1);
        opts.Filter.Filters[0].Field.Should().Be("Name");
        opts.Filter.Filters[0].Operator.Should().Be(FilterOperators.Contains);
        opts.Filter.Filters[0].Value.Should().Be("john");
        opts.Paging.Page.Should().Be(1);
        opts.Paging.PageSize.Should().Be(10);
    }

    [Fact]
    public void Generic_MultipleFilters_AllParsed()
    {
        var opts = Parse(new()
        {
            ["filter[0].field"]    = "Name",
            ["filter[0].operator"] = "contains",
            ["filter[0].value"]    = "Alice",
            ["filter[1].field"]    = "Age",
            ["filter[1].operator"] = "gt",
            ["filter[1].value"]    = "25",
            ["logic"]              = "and"
        });

        opts.Filter!.Filters.Should().HaveCount(2);
        opts.Filter.Logic.Should().Be(LogicOperator.And);
        opts.Filter.Filters[1].Operator.Should().Be(FilterOperators.GreaterThan);
    }

    [Fact]
    public void Generic_Sort_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["sort[0].field"] = "Age",
            ["sort[0].desc"]  = "true",
            ["sort[1].field"] = "Name",
            ["sort[1].desc"]  = "false"
        });

        opts.Sort.Should().HaveCount(2);
        opts.Sort[0].Field.Should().Be("Age");
        opts.Sort[0].Descending.Should().BeTrue();
        opts.Sort[1].Field.Should().Be("Name");
        opts.Sort[1].Descending.Should().BeFalse();
    }

    [Fact]
    public void Generic_SortString_MultiField_ParsedCorrectly()
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
    public void Generic_SortString_NestedFields_ParsedCorrectly()
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
    public void Generic_SortString_InvalidDirection_DefaultsToAsc()
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
    public void Generic_SortString_Aggregate_ParsedCorrectly()
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
    public void Generic_Select_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["select"] = "Name,Email,Age"
        });

        opts.Select.Should().BeEquivalentTo(["Name", "Email", "Age"]);
    }

    [Fact]
    public void Generic_GroupAndAggregateSelect_ParsedCorrectly()
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
    public void Generic_OrLogic_SetCorrectly()
    {
        var opts = Parse(new()
        {
            ["filter[0].field"] = "City",
            ["filter[0].value"] = "London",
            ["logic"]           = "or"
        });

        opts.Filter!.Logic.Should().Be(LogicOperator.Or);
    }

    [Fact]
    public void Generic_EmptyInput_ReturnsDefaultOptions()
    {
        var opts = Parse(new());

        opts.Filter.Should().BeNull();
        opts.Sort.Should().BeEmpty();
        opts.Select.Should().BeNull();
        opts.Paging.Page.Should().Be(1);
        opts.Paging.PageSize.Should().Be(20);
    }

    // ════════════════════════════════════════════════════════════════════
    // 2. JSON Filter Format
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void JsonFilter_SingleCondition_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["filter"] = """{"logic":"and","filters":[{"field":"Name","operator":"contains","value":"john"}]}"""
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Logic.Should().Be(LogicOperator.And);
        opts.Filter.Filters.Should().HaveCount(1);
        opts.Filter.Filters[0].Field.Should().Be("Name");
        opts.Filter.Filters[0].Operator.Should().Be(FilterOperators.Contains);
        opts.Filter.Filters[0].Value.Should().Be("john");
    }

    [Fact]
    public void JsonFilter_MultipleConditions_AllParsed()
    {
        var opts = Parse(new()
        {
            ["filter"] = """
            {
              "logic": "or",
              "filters": [
                {"field": "City",  "operator": "eq",  "value": "London"},
                {"field": "Age",   "operator": "gt",  "value": "30"}
              ]
            }
            """
        });

        opts.Filter!.Logic.Should().Be(LogicOperator.Or);
        opts.Filter.Filters.Should().HaveCount(2);
    }

    [Fact]
    public void JsonFilter_NestedGroup_ParsedRecursively()
    {
        var opts = Parse(new()
        {
            ["filter"] = """
            {
              "logic": "and",
              "filters": [
                {"field": "Age", "operator": "gt", "value": "20"},
                {
                  "logic": "or",
                  "filters": [
                    {"field": "City", "operator": "eq", "value": "London"},
                    {"field": "City", "operator": "eq", "value": "Berlin"}
                  ]
                }
              ]
            }
            """
        });

        opts.Filter!.Filters.Should().HaveCount(1);
        opts.Filter.Groups.Should().HaveCount(1);
        opts.Filter.Groups[0].Logic.Should().Be(LogicOperator.Or);
        opts.Filter.Groups[0].Filters.Should().HaveCount(2);
    }

    [Fact]
    public void JsonFilter_MalformedJson_IsIgnoredGracefully()
    {
        var opts = Parse(new()
        {
            ["filter"] = "{ this is not valid json"
        });

        opts.Filter.Should().BeNull();
    }

    // ════════════════════════════════════════════════════════════════════
    // 3. Syncfusion Format
    // ════════════════════════════════════════════════════════════════════

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
    // 5. JQL-lite Format (?query=...)
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void Jql_SimpleCondition_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["query"] = "name = \"john\""
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Logic.Should().Be(LogicOperator.And);
        opts.Filter.Filters.Should().ContainSingle();
        opts.Filter.Filters[0].Field.Should().Be("name");
        opts.Filter.Filters[0].Operator.Should().Be(FilterOperators.Equal);
        opts.Filter.Filters[0].Value.Should().Be("john");
    }

    [Fact]
    public void Jql_AndCondition_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["query"] = "name = \"john\" AND age >= 20"
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Logic.Should().Be(LogicOperator.And);
        opts.Filter.Filters.Should().HaveCount(2);
        opts.Filter.Filters.Should().Contain(f => f.Field == "name" && f.Operator == FilterOperators.Equal && f.Value == "john");
        opts.Filter.Filters.Should().Contain(f => f.Field == "age" && f.Operator == FilterOperators.GreaterThanOrEq && f.Value == "20");
    }

    [Fact]
    public void Jql_OrCondition_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["query"] = "name = \"john\" OR name = \"doe\""
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Logic.Should().Be(LogicOperator.Or);
        opts.Filter.Filters.Should().HaveCount(2);
        opts.Filter.Filters.Should().Contain(f => f.Field == "name" && f.Value == "john");
        opts.Filter.Filters.Should().Contain(f => f.Field == "name" && f.Value == "doe");
    }

    [Fact]
    public void Jql_NestedParentheses_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["query"] = "(name = \"john\" OR name = \"doe\") AND age > 18"
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Logic.Should().Be(LogicOperator.And);
        opts.Filter.Filters.Should().ContainSingle(f => f.Field == "age" && f.Operator == FilterOperators.GreaterThan && f.Value == "18");
        opts.Filter.Groups.Should().ContainSingle();
        opts.Filter.Groups[0].Logic.Should().Be(LogicOperator.Or);
        opts.Filter.Groups[0].Filters.Should().HaveCount(2);
    }

    [Fact]
    public void Jql_InOperator_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["query"] = "status IN (\"active\",\"pending\")"
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Filters.Should().ContainSingle();
        opts.Filter.Filters[0].Field.Should().Be("status");
        opts.Filter.Filters[0].Operator.Should().Be(FilterOperators.In);
        opts.Filter.Filters[0].Value.Should().Be("active,pending");
    }

    [Fact]
    public void Jql_NestedPropertyPath_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["query"] = "orders.customer.name CONTAINS \"john\""
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Filters.Should().ContainSingle();
        opts.Filter.Filters[0].Field.Should().Be("orders.customer.name");
        opts.Filter.Filters[0].Operator.Should().Be(FilterOperators.Contains);
        opts.Filter.Filters[0].Value.Should().Be("john");
    }

    [Fact]
    public void Jql_EmailAndNestedNumericCondition_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["query"] = "email = \"ops@acmeretail.com\" AND orders.number = \"ORD-2026-0002\" AND orders.items.quantity > 2"
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Logic.Should().Be(LogicOperator.And);
        opts.Filter.Filters.Should().HaveCount(3);
        opts.Filter.Filters.Should().Contain(f => f.Field == "email" && f.Operator == FilterOperators.Equal && f.Value == "ops@acmeretail.com");
        opts.Filter.Filters.Should().Contain(f => f.Field == "orders.number" && f.Operator == FilterOperators.Equal && f.Value == "ORD-2026-0002");
        opts.Filter.Filters.Should().Contain(f => f.Field == "orders.items.quantity" && f.Operator == FilterOperators.GreaterThan && f.Value == "2");
    }

    [Fact]
    public void Jql_InvalidSyntax_Throws()
    {
        Action act = () => Parse(new()
        {
            ["query"] = "name = "
        });

        act.Should().Throw<JqlParseException>();
    }
}
