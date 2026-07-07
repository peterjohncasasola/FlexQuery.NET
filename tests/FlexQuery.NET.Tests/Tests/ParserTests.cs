using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Jql;
using Microsoft.Extensions.Primitives;

namespace FlexQuery.NET.Tests.Tests;

/// <summary>
/// Tests for QueryOptionsParser covering all query-string formats
/// and JqlParser for JQL-lite syntax.
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

    private static FilterGroup JqlParse(string jql) =>
        new JqlQueryParser().Parse(jql);

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

    [Fact]
    public void JsonFilter_ExplicitJsonSyntax_ParsedCorrectly()
    {
        var opts = QueryOptionsParser.Parse(
            new FlexQueryParameters
            {
                Filter = """{"logic":"or","filters":[{"field":"City","operator":"eq","value":"London"},{"field":"Age","operator":"gt","value":"30"}]}"""
            },
            QuerySyntax.Json);

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Logic.Should().Be(LogicOperator.Or);
        opts.Filter.Filters.Should().HaveCount(2);
        opts.Filter.Filters.Should().Contain(f => f.Field == "City" && f.Operator == FilterOperators.Equal && f.Value == "London");
        opts.Filter.Filters.Should().Contain(f => f.Field == "Age" && f.Operator == FilterOperators.GreaterThan && f.Value == "30");
    }

    [Fact]
    public void JsonFilter_ExplicitDslSyntax_StillParsesJsonPayload()
    {
        var opts = QueryOptionsParser.Parse(
            new FlexQueryParameters
            {
                Filter = """{"logic":"and","filters":[{"field":"Name","operator":"contains","value":"john"}]}"""
            },
            QuerySyntax.NativeDsl);

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Logic.Should().Be(LogicOperator.And);
        opts.Filter.Filters.Should().ContainSingle(f =>
            f.Field == "Name" && f.Operator == FilterOperators.Contains && f.Value == "john");
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
    // 5. JQL-lite Format (via JqlParser from FlexQuery.NET.Parsers.Jql)
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void Jql_SimpleCondition_ParsedCorrectly()
    {
        var filter = JqlParse("name = \"john\"");

        filter.Logic.Should().Be(LogicOperator.And);
        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("name");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Equal);
        filter.Filters[0].Value.Should().Be("john");
    }

    [Fact]
    public void Jql_AndCondition_ParsedCorrectly()
    {
        var filter = JqlParse("name = \"john\" AND age >= 20");

        filter.Logic.Should().Be(LogicOperator.And);
        filter.Filters.Should().HaveCount(2);
        filter.Filters.Should().Contain(f => f.Field == "name" && f.Operator == FilterOperators.Equal && f.Value == "john");
        filter.Filters.Should().Contain(f => f.Field == "age" && f.Operator == FilterOperators.GreaterThanOrEq && f.Value == "20");
    }

    [Fact]
    public void Jql_OrCondition_ParsedCorrectly()
    {
        var filter = JqlParse("name = \"john\" OR name = \"doe\"");

        filter.Logic.Should().Be(LogicOperator.Or);
        filter.Filters.Should().HaveCount(2);
        filter.Filters.Should().Contain(f => f.Field == "name" && f.Value == "john");
        filter.Filters.Should().Contain(f => f.Field == "name" && f.Value == "doe");
    }

    [Fact]
    public void Jql_NestedParentheses_ParsedCorrectly()
    {
        var filter = JqlParse("(name = \"john\" OR name = \"doe\") AND age > 18");

        filter.Logic.Should().Be(LogicOperator.And);
        filter.Filters.Should().ContainSingle(f => f.Field == "age" && f.Operator == FilterOperators.GreaterThan && f.Value == "18");
        filter.Groups.Should().ContainSingle();
        filter.Groups[0].Logic.Should().Be(LogicOperator.Or);
        filter.Groups[0].Filters.Should().HaveCount(2);
    }

    [Fact]
    public void Jql_InOperator_ParsedCorrectly()
    {
        var filter = JqlParse("status IN (\"active\",\"pending\")");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("status");
        filter.Filters[0].Operator.Should().Be(FilterOperators.In);
        filter.Filters[0].Value.Should().Be("active,pending");
    }

    [Fact]
    public void Jql_NestedPropertyPath_ParsedCorrectly()
    {
        var filter = JqlParse("orders.customer.name CONTAINS \"john\"");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("orders.customer.name");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Contains);
        filter.Filters[0].Value.Should().Be("john");
    }

    [Fact]
    public void Jql_EmailAndNestedNumericCondition_ParsedCorrectly()
    {
        var filter = JqlParse("email = \"ops@acmeretail.com\" AND orders.number = \"ORD-2026-0002\" AND orders.items.quantity > 2");

        filter.Logic.Should().Be(LogicOperator.And);
        filter.Filters.Should().HaveCount(3);
        filter.Filters.Should().Contain(f => f.Field == "email" && f.Operator == FilterOperators.Equal && f.Value == "ops@acmeretail.com");
        filter.Filters.Should().Contain(f => f.Field == "orders.number" && f.Operator == FilterOperators.Equal && f.Value == "ORD-2026-0002");
        filter.Filters.Should().Contain(f => f.Field == "orders.items.quantity" && f.Operator == FilterOperators.GreaterThan && f.Value == "2");
    }

    [Fact]
    public void Jql_BetweenOperator_ParsedCorrectly()
    {
        var filter = JqlParse("age BETWEEN 18 AND 60");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("age");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Between);
        filter.Filters[0].Value.Should().Be("18,60");
    }

    [Fact]
    public void Jql_IsNullOperator_ParsedCorrectly()
    {
        var filter = JqlParse("deletedAt IS NULL");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("deletedAt");
        filter.Filters[0].Operator.Should().Be(FilterOperators.IsNull);
        filter.Filters[0].Value.Should().BeNull();
    }

    [Fact]
    public void Jql_IsNotNullOperator_ParsedCorrectly()
    {
        var filter = JqlParse("deletedAt IS NOT NULL");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("deletedAt");
        filter.Filters[0].Operator.Should().Be(FilterOperators.IsNotNull);
        filter.Filters[0].Value.Should().BeNull();
    }

    [Fact]
    public void Jql_LikeOperator_ParsedCorrectly()
    {
        var filter = JqlParse("name LIKE \"%john%\"");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("name");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Like);
        filter.Filters[0].Value.Should().Be("%john%");
    }

    [Fact]
    public void Jql_AnyOperator_ParsedCorrectly()
    {
        var filter = JqlParse("orders ANY total > 1000");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("orders");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Any);
        filter.Filters[0].Value.Should().Be("total:gt:1000");
    }

    [Fact]
    public void Jql_AllOperator_ParsedCorrectly()
    {
        var filter = JqlParse("orders ALL status = \"completed\"");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("orders");
        filter.Filters[0].Operator.Should().Be(FilterOperators.All);
        filter.Filters[0].Value.Should().Be("status:eq:completed");
    }

    [Fact]
    public void Jql_CountOperator_ParsedCorrectly()
    {
        var filter = JqlParse("orders COUNT > 5");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("orders");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Count);
        filter.Filters[0].Value.Should().Be("gt:5");
    }

    // ════════════════════════════════════════════════════════════════════
    // 6. Scoped Collection Filters (via JqlParser)
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void Jql_ScopedAny_DotSyntax_ParsedToScopedFilter()
    {
        var filter = JqlParse("orders.any(status = \"Cancelled\")");

        filter.Filters.Should().ContainSingle();
        var cond = filter.Filters[0];
        cond.Field.Should().Be("orders");
        cond.Operator.Should().Be(FilterOperators.Any);
        cond.ScopedFilter.Should().NotBeNull();
        cond.ScopedFilter!.Filters.Should().ContainSingle(f => f.Field == "status" && f.Value == "Cancelled");
    }

    [Fact]
    public void Jql_ScopedAll_DotSyntax_ParsedToScopedFilter()
    {
        var filter = JqlParse("orders.all(status = \"Active\")");

        filter.Filters.Should().ContainSingle();
        var cond = filter.Filters[0];
        cond.Field.Should().Be("orders");
        cond.Operator.Should().Be(FilterOperators.All);
        cond.ScopedFilter.Should().NotBeNull();
        cond.ScopedFilter!.Filters.Should().ContainSingle(f => f.Field == "status" && f.Value == "Active");
    }

    [Fact]
    public void Jql_ScopedBracket_ParsedAsAnyCollectionNode()
    {
        var filter = JqlParse("orders[status = \"Cancelled\"]");

        filter.Filters.Should().ContainSingle();
        var cond = filter.Filters[0];
        cond.Field.Should().Be("orders");
        cond.Operator.Should().Be(FilterOperators.Any);
        cond.ScopedFilter.Should().NotBeNull();
        cond.ScopedFilter!.Filters.Should().ContainSingle(f => f.Field == "status" && f.Value == "Cancelled");
    }

    [Fact]
    public void Jql_ScopedAny_MultipleConditions_ParsedAsAndLogicalInsideCollection()
    {
        var filter = JqlParse("orders.any(status = \"Cancelled\" AND total > 500)");

        filter.Filters.Should().ContainSingle();
        var cond = filter.Filters[0];
        cond.Field.Should().Be("orders");
        cond.Operator.Should().Be(FilterOperators.Any);
        cond.ScopedFilter.Should().NotBeNull();

        var inner = cond.ScopedFilter!;
        inner.Logic.Should().Be(LogicOperator.And);
        inner.Filters.Should().HaveCount(2);
        inner.Filters.Should().Contain(f => f.Field == "status" && f.Value == "Cancelled");
        inner.Filters.Should().Contain(f => f.Field == "total" && f.Operator == FilterOperators.GreaterThan);
    }

    [Fact]
    public void Jql_ScopedAny_WithOrInsideGroup_ParsedCorrectly()
    {
        var filter = JqlParse("orders.any(status = \"Cancelled\" OR status = \"Refunded\")");

        filter.Filters.Should().ContainSingle();
        var cond = filter.Filters[0];
        cond.ScopedFilter.Should().NotBeNull();
        cond.ScopedFilter!.Logic.Should().Be(LogicOperator.Or);
        cond.ScopedFilter.Filters.Should().HaveCount(2);
    }

    [Fact]
    public void Jql_NestedScopedCollections_ParsedRecursively()
    {
        var filter = JqlParse("orders.any(status = \"Cancelled\" AND orderItems.any(id = 101))");

        filter.Filters.Should().ContainSingle();
        var outer = filter.Filters[0];
        outer.Field.Should().Be("orders");
        outer.Operator.Should().Be(FilterOperators.Any);
        outer.ScopedFilter.Should().NotBeNull();

        var innerGroup = outer.ScopedFilter!;
        innerGroup.Logic.Should().Be(LogicOperator.And);
        innerGroup.Filters.Should().HaveCount(2);

        innerGroup.Filters.Should().Contain(f => f.Field == "status").Subject.Value.Should().Be("Cancelled");

        var nested = innerGroup.Filters.Should().Contain(f => f.Field == "orderItems").Subject;
        nested.Operator.Should().Be(FilterOperators.Any);
        nested.ScopedFilter.Should().NotBeNull();
        nested.ScopedFilter!.Filters.Should().ContainSingle(f => f.Field == "id" && f.Value == "101");
    }

    [Fact]
    public void Jql_ScopedAny_CombinedWithFlatCondition_ParsedToAndLogical()
    {
        var filter = JqlParse("name = \"Alice\" AND orders.any(total > 1000)");

        filter.Logic.Should().Be(LogicOperator.And);
        filter.Filters.Should().HaveCount(2);
        filter.Filters.Should().Contain(f => f.Field == "name" && f.Operator == FilterOperators.Equal && f.Value == "Alice");

        var coll = filter.Filters.Should().Contain(f => f.Field == "orders").Subject;
        coll.Operator.Should().Be(FilterOperators.Any);
        coll.ScopedFilter.Should().NotBeNull();
    }

    [Fact]
    public void Jql_ScopedAny_ConvertsToFilterConditionWithScopedFilter()
    {
        var filter = JqlParse("orders.any(status = \"Cancelled\" AND total > 500)");

        filter.Logic.Should().Be(LogicOperator.And);
        filter.Filters.Should().ContainSingle();

        var cond = filter.Filters[0];
        cond.Field.Should().Be("orders");
        cond.Operator.Should().Be(FilterOperators.Any);
        cond.Value.Should().BeNull();
        cond.ScopedFilter.Should().NotBeNull();

        var inner = cond.ScopedFilter!;
        inner.Logic.Should().Be(LogicOperator.And);
        inner.Filters.Should().HaveCount(2);
        inner.Filters.Should().Contain(f => f.Field == "status" && f.Value == "Cancelled");
        inner.Filters.Should().Contain(f => f.Field == "total" && f.Operator == FilterOperators.GreaterThan);
    }

    [Fact]
    public void Jql_BracketSyntax_ConvertsToFilterConditionWithScopedFilter()
    {
        var filter = JqlParse("orders[status = \"Cancelled\"]");

        filter.Filters.Should().ContainSingle();
        var cond = filter.Filters[0];
        cond.Field.Should().Be("orders");
        cond.Operator.Should().Be(FilterOperators.Any);
        cond.ScopedFilter.Should().NotBeNull();
        cond.ScopedFilter!.Filters.Should().ContainSingle(f => f.Field == "status");
    }

    [Fact]
    public void Jql_NestedScopedCollections_ConvertsToNestedScopedFilters()
    {
        var filter = JqlParse("orders.any(status = \"Cancelled\" AND orderItems.any(id = 101))");

        filter.Filters.Should().ContainSingle();
        var outer = filter.Filters[0];
        outer.Field.Should().Be("orders");
        outer.ScopedFilter.Should().NotBeNull();

        var innerGroup = outer.ScopedFilter!;
        innerGroup.Logic.Should().Be(LogicOperator.And);
        innerGroup.Filters.Should().HaveCount(2);

        var statusCond = innerGroup.Filters.Should()
            .Contain(f => f.Field == "status").Subject;
        statusCond.Value.Should().Be("Cancelled");

        var nestedCond = innerGroup.Filters.Should()
            .Contain(f => f.Field == "orderItems").Subject;
        nestedCond.ScopedFilter.Should().NotBeNull();
        nestedCond.ScopedFilter!.Filters.Should()
            .ContainSingle(f => f.Field == "id" && f.Value == "101");
    }

    [Fact]
    public void Jql_FlatConditions_StillWorkAfterScopedSupport()
    {
        var filter = JqlParse("orders.customer.name CONTAINS \"john\"");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("orders.customer.name");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Contains);
        filter.Filters[0].Value.Should().Be("john");
        filter.Filters[0].ScopedFilter.Should().BeNull();
    }

    // ════════════════════════════════════════════════════════════════════
    // HAVING Parser Tests
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void Having_Parse_FieldLessCount_ColonFormat()
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
    public void Having_Parse_FieldLessCount_ParenthesisFormat()
    {
        var opts = Parse(new()
        {
            ["group"] = "status",
            ["select"] = "status,count()",
            ["having"] = "count():gt:20"
        });

        opts.Having.Should().NotBeNull();
        opts.Having!.Function.Should().Be("count");
        opts.Having.Field.Should().BeNull();
        opts.Having.Operator.Should().Be(FilterOperators.GreaterThan);
        opts.Having.Value.Should().Be("20");
    }

    [Fact]
    public void Having_Parse_WithField_ColonFormat()
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
    public void Having_Parse_WithField_ParenthesisFormat()
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

    [Fact]
    public void Having_Parse_CountField_ColonFormat()
    {
        var opts = Parse(new()
        {
            ["group"] = "status",
            ["select"] = "status,count(id)",
            ["having"] = "count:id:gt:5"
        });

        opts.Having.Should().NotBeNull();
        opts.Having!.Function.Should().Be("count");
        opts.Having.Field.Should().Be("id");
        opts.Having.Operator.Should().Be(FilterOperators.GreaterThan);
        opts.Having.Value.Should().Be("5");
    }

    [Fact]
    public void Having_AggregateAlias_FieldLessCount_ReturnsCount()
    {
        var alias = ParserUtilities.BuildAggregateAlias("count", null);
        alias.Should().Be("Count");
    }

    [Fact]
    public void Having_AggregateAlias_FieldLessSum_ReturnsSum()
    {
        var alias = ParserUtilities.BuildAggregateAlias("sum", null);
        alias.Should().Be("Sum");
    }

    [Fact]
    public void Having_AggregateAlias_WithField_ReturnsFieldPrefixPlusFunction()
    {
        var alias = ParserUtilities.BuildAggregateAlias("sum", "total");
        alias.Should().Be("totalSum");
    }

    [Fact]
    public void Having_AggregateAlias_WithMultiWordField_ReturnsCamelCase()
    {
        var alias = ParserUtilities.BuildAggregateAlias("sum", "grand_total");
        alias.Should().Be("grandTotalSum");
    }
}
