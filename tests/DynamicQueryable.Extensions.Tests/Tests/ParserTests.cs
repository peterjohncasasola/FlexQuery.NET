using DynamicQueryable.Constants;
using DynamicQueryable.Models;
using DynamicQueryable.Parsers;
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
    public void Generic_Select_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["select"] = "Name,Email,Age"
        });

        opts.Select.Should().BeEquivalentTo(["Name", "Email", "Age"]);
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
    public void Syncfusion_Filter_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["where[0][field]"]    = "Name",
            ["where[0][operator]"] = "contains",
            ["where[0][value]"]    = "john",
            ["skip"]               = "0",
            ["take"]               = "10"
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Filters.Should().HaveCount(1);
        opts.Filter.Filters[0].Field.Should().Be("Name");
        opts.Filter.Filters[0].Operator.Should().Be(FilterOperators.Contains);
        opts.Paging.PageSize.Should().Be(10);
        opts.Paging.Page.Should().Be(1);
    }

    [Fact]
    public void Syncfusion_SkipTake_ConvertedToPaging()
    {
        var opts = Parse(new()
        {
            ["skip"] = "20",
            ["take"] = "10"
        });

        opts.Paging.Page.Should().Be(3);     // skip=20, take=10 → page 3
        opts.Paging.PageSize.Should().Be(10);
    }

    [Fact]
    public void Syncfusion_Sort_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["sorted[0][name]"]      = "Age",
            ["sorted[0][direction]"] = "descending",
            ["skip"]                 = "0",
            ["take"]                 = "5"
        });

        opts.Sort.Should().HaveCount(1);
        opts.Sort[0].Field.Should().Be("Age");
        opts.Sort[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void Syncfusion_AscendingSort_ParsedCorrectly()
    {
        var opts = Parse(new()
        {
            ["sorted[0][name]"]      = "Name",
            ["sorted[0][direction]"] = "ascending",
            ["skip"]                 = "0",
            ["take"]                 = "10"
        });

        opts.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Syncfusion_MultipleFilters_AllParsed()
    {
        var opts = Parse(new()
        {
            ["where[0][field]"]    = "City",
            ["where[0][operator]"] = "equal",
            ["where[0][value]"]    = "London",
            ["where[1][field]"]    = "Age",
            ["where[1][operator]"] = "greaterthan",
            ["where[1][value]"]    = "25",
            ["skip"]               = "0",
            ["take"]               = "10"
        });

        opts.Filter!.Filters.Should().HaveCount(2);
        opts.Filter.Filters[0].Operator.Should().Be(FilterOperators.Equal);
        opts.Filter.Filters[1].Operator.Should().Be(FilterOperators.GreaterThan);
    }

    // ════════════════════════════════════════════════════════════════════
    // 4. Laravel Spatie Format
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void Spatie_Filter_MappedAsEqCondition()
    {
        var opts = Parse(new()
        {
            ["filter[name]"] = "john",
            ["filter[age]"]  = "25"
        });

        opts.Filter.Should().NotBeNull();
        opts.Filter!.Filters.Should().HaveCount(2);
        opts.Filter.Filters.Should().AllSatisfy(f => f.Operator.Should().Be(FilterOperators.Equal));

        var nameFilter = opts.Filter.Filters.First(f => f.Field == "name");
        nameFilter.Value.Should().Be("john");
    }

    [Fact]
    public void Spatie_Sort_AscendingWithoutPrefix()
    {
        var opts = Parse(new()
        {
            ["filter[city]"] = "London",
            ["sort"]         = "name"
        });

        opts.Sort.Should().HaveCount(1);
        opts.Sort[0].Field.Should().Be("name");
        opts.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void Spatie_Sort_DescendingWithDashPrefix()
    {
        var opts = Parse(new()
        {
            ["filter[city]"] = "London",
            ["sort"]         = "-created_at"
        });

        opts.Sort[0].Field.Should().Be("created_at");
        opts.Sort[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void Spatie_Sort_MultipleMixed()
    {
        var opts = Parse(new()
        {
            ["filter[city]"] = "London",
            ["sort"]         = "-age,name"
        });

        opts.Sort.Should().HaveCount(2);
        opts.Sort[0].Field.Should().Be("age");
        opts.Sort[0].Descending.Should().BeTrue();
        opts.Sort[1].Field.Should().Be("name");
        opts.Sort[1].Descending.Should().BeFalse();
    }

    [Fact]
    public void Spatie_Fields_MappedToSelect()
    {
        var opts = Parse(new()
        {
            ["filter[city]"]       = "London",
            ["fields[users]"]      = "name,email"
        });

        opts.Select.Should().BeEquivalentTo(["name", "users.name", "email", "users.email"]);
    }

    [Fact]
    public void Spatie_Include_MappedToIncludes()
    {
        var opts = Parse(new()
        {
            ["filter[city]"] = "London",
            ["include"]      = "roles,permissions"
        });

        opts.Includes.Should().BeEquivalentTo(["roles", "permissions"]);
    }

    [Fact]
    public void Spatie_InvalidKeys_AreIgnoredGracefully()
    {
        var opts = Parse(new()
        {
            ["filter[name]"] = "john",
            ["garbage_key"]  = "garbage_value",
            ["!!!"]          = "bad"
        });

        opts.Filter!.Filters.Should().HaveCount(1);
    }
}
