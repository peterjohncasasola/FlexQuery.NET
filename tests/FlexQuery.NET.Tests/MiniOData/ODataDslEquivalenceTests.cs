using FlexQuery.NET.Constants;
using FlexQuery.NET.Parsers.MiniOData;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Dsl;
using FluentAssertions;

namespace FlexQuery.NET.Tests.MiniOData;

/// <summary>
/// Equivalence tests verifying that Native DSL and Mini OData syntaxes
/// produce semantically identical AST structures.
/// This is the core contract: same query semantics regardless of input syntax.
/// </summary>
public class ODataDslEquivalenceTests
{
    // ========================
    // Simple Equality
    // ========================

    [Fact]
    public void Equality_NativeDsl_And_OData_ProduceEquivalentAst()
    {
        // Native DSL
        var dslFilter = ParseDsl("name:eq:john");

        // Mini OData
        var odataFilter = ODataFilterParser.Parse("name eq 'john'");

        AssertEquivalentFilter(dslFilter, odataFilter, "name", FilterOperators.Equal, "john");
    }

    // ========================
    // Contains
    // ========================

    [Fact]
    public void Contains_NativeDsl_And_OData_ProduceEquivalentAst()
    {
        var dslFilter = ParseDsl("name:contains:john");
        var odataFilter = ODataFilterParser.Parse("contains(name,'john')");

        AssertEquivalentFilter(dslFilter, odataFilter, "name", FilterOperators.Contains, "john");
    }

    // ========================
    // StartsWith
    // ========================

    [Fact]
    public void StartsWith_NativeDsl_And_OData_ProduceEquivalentAst()
    {
        var dslFilter = ParseDsl("name:startswith:jo");
        var odataFilter = ODataFilterParser.Parse("startswith(name,'jo')");

        AssertEquivalentFilter(dslFilter, odataFilter, "name", FilterOperators.StartsWith, "jo");
    }

    // ========================
    // EndsWith
    // ========================

    [Fact]
    public void EndsWith_NativeDsl_And_OData_ProduceEquivalentAst()
    {
        var dslFilter = ParseDsl("email:endswith:.com");
        var odataFilter = ODataFilterParser.Parse("endswith(email,'.com')");

        AssertEquivalentFilter(dslFilter, odataFilter, "email", FilterOperators.EndsWith, ".com");
    }

    // ========================
    // Greater Than
    // ========================

    [Fact]
    public void GreaterThan_NativeDsl_And_OData_ProduceEquivalentAst()
    {
        var dslFilter = ParseDsl("age:gt:18");
        var odataFilter = ODataFilterParser.Parse("age gt 18");

        AssertEquivalentFilter(dslFilter, odataFilter, "age", FilterOperators.GreaterThan, "18");
    }

    // ========================
    // Compound AND
    // ========================

    [Fact]
    public void CompoundAnd_NativeDsl_And_OData_ProduceEquivalentAst()
    {
        var dslFilter = ParseDsl("age:gt:18&status:eq:active");
        var odataFilter = ODataFilterParser.Parse("age gt 18 and status eq 'active'");

        // Both should produce AND groups with 2 conditions
        dslFilter.Logic.Should().Be(LogicOperator.And);
        odataFilter.Logic.Should().Be(LogicOperator.And);

        dslFilter.Filters.Should().HaveCount(2);
        odataFilter.Filters.Should().HaveCount(2);

        // Verify field names match
        dslFilter.Filters[0].Field.Should().Be(odataFilter.Filters[0].Field);
        dslFilter.Filters[1].Field.Should().Be(odataFilter.Filters[1].Field);

        // Verify operators match
        dslFilter.Filters[0].Operator.Should().Be(odataFilter.Filters[0].Operator);
        dslFilter.Filters[1].Operator.Should().Be(odataFilter.Filters[1].Operator);
    }

    // ========================
    // Compound OR
    // ========================

    [Fact]
    public void CompoundOr_NativeDsl_And_OData_ProduceEquivalentAst()
    {
        var dslFilter = ParseDsl("status:eq:active|status:eq:pending");
        var odataFilter = ODataFilterParser.Parse("status eq 'active' or status eq 'pending'");

        dslFilter.Logic.Should().Be(LogicOperator.Or);
        odataFilter.Logic.Should().Be(LogicOperator.Or);

        dslFilter.Filters.Should().HaveCount(2);
        odataFilter.Filters.Should().HaveCount(2);
    }

    // ========================
    // Null Check
    // ========================

    [Fact]
    public void NullCheck_NativeDsl_And_OData_ProduceEquivalentAst()
    {
        var dslFilter = ParseDsl("deletedAt:isnull");
        var odataFilter = ODataFilterParser.Parse("deletedAt eq null");

        dslFilter.Filters[0].Field.Should().Be(odataFilter.Filters[0].Field);
        dslFilter.Filters[0].Operator.Should().Be(FilterOperators.IsNull);
        odataFilter.Filters[0].Operator.Should().Be(FilterOperators.IsNull);
    }

    // ========================
    // Any (Relationship)
    // ========================

    [Fact]
    public void AnyRelationship_NativeDsl_And_OData_ProduceEquivalentAst()
    {
        var dslFilter = ParseDsl("orders.any(status:eq:Cancelled)");
        var odataFilter = ODataFilterParser.Parse("orders/any(o: o/status eq 'Cancelled')");

        // Both should produce a FilterCondition with operator = "any"
        dslFilter.Filters[0].Field.Should().Be("orders");
        odataFilter.Filters[0].Field.Should().Be("orders");

        dslFilter.Filters[0].Operator.Should().Be("any");
        odataFilter.Filters[0].Operator.Should().Be("any");

        // Both should have a scoped filter
        dslFilter.Filters[0].ScopedFilter.Should().NotBeNull();
        odataFilter.Filters[0].ScopedFilter.Should().NotBeNull();

        // Inner scoped filter: status eq 'Cancelled'
        dslFilter.Filters[0].ScopedFilter!.Filters[0].Field.Should().Be("status");
        odataFilter.Filters[0].ScopedFilter!.Filters[0].Field.Should().Be("status");

        dslFilter.Filters[0].ScopedFilter!.Filters[0].Value.Should().Be("Cancelled");
        odataFilter.Filters[0].ScopedFilter!.Filters[0].Value.Should().Be("Cancelled");
    }

    // ========================
    // Negation
    // ========================

    [Fact]
    public void Negation_NativeDsl_And_OData_ProduceEquivalentAst()
    {
        var dslFilter = ParseDsl("!(status:eq:deleted)");
        var odataFilter = ODataFilterParser.Parse("not (status eq 'deleted')");

        dslFilter.IsNegated.Should().BeTrue();
        odataFilter.IsNegated.Should().BeTrue();

        dslFilter.Filters[0].Field.Should().Be("status");
        odataFilter.Filters[0].Field.Should().Be("status");
    }

    // ========================
    // OrderBy Equivalence
    // ========================

    [Fact]
    public void OrderBy_NativeDsl_And_OData_ProduceEquivalentSortNodes()
    {
        // Native DSL: sort=createdAt:desc
        var nativeParams = new FlexQueryParameters { Sort = "createdAt:desc" };
        var nativeOptions = QueryOptionsParser.Parse(nativeParams);

        // Mini OData: $orderby=createdAt desc
        var odataParams = new Dictionary<string, string> { ["$orderby"] = "createdAt desc" };
        var odataOptions = ODataQueryParameterParser.Parse(odataParams);

        nativeOptions.Sort.Should().HaveCount(1);
        odataOptions.Sort.Should().HaveCount(1);

        nativeOptions.Sort[0].Field.Should().Be(odataOptions.Sort[0].Field);
        nativeOptions.Sort[0].Descending.Should().Be(odataOptions.Sort[0].Descending);
    }

    // ========================
    // Select Equivalence
    // ========================

    [Fact]
    public void Select_NativeDsl_And_OData_ProduceEquivalentProjection()
    {
        // Native DSL: select=id,name,email
        var nativeParams = new FlexQueryParameters { Select = "id,name,email" };
        var nativeOptions = QueryOptionsParser.Parse(nativeParams);

        // Mini OData: $select=id,name,email
        var odataParams = new Dictionary<string, string> { ["$select"] = "id,name,email" };
        var odataOptions = ODataQueryParameterParser.Parse(odataParams);

        nativeOptions.Select.Should().BeEquivalentTo(odataOptions.Select);
    }

    // ========================
    // Pagination Equivalence
    // ========================

    [Fact]
    public void Pagination_NativeDsl_And_OData_ProduceEquivalentPaging()
    {
        // Native DSL: page=3&pageSize=10
        var nativeParams = new FlexQueryParameters { Page = 3, PageSize = 10 };
        var nativeOptions = QueryOptionsParser.Parse(nativeParams);

        // Mini OData: $top=10&$skip=20 (page 3 with size 10 = skip 20)
        var odataParams = new Dictionary<string, string>
        {
            ["$top"] = "10",
            ["$skip"] = "20"
        };
        var odataOptions = ODataQueryParameterParser.Parse(odataParams);

        nativeOptions.Paging.PageSize.Should().Be(odataOptions.Paging.PageSize);
        nativeOptions.Paging.Page.Should().Be(odataOptions.Paging.Page);
    }

    // ========================
    // Include / Expand Equivalence
    // ========================

    [Fact]
    public void Include_NativeDsl_And_OData_ProduceEquivalentIncludes()
    {
        // Native DSL: include=orders,profile
        var nativeParams = new FlexQueryParameters { Include = "orders,profile" };
        var nativeOptions = QueryOptionsParser.Parse(nativeParams);

        // Mini OData: $expand=orders,profile
        var odataParams = new Dictionary<string, string> { ["$expand"] = "orders,profile" };
        var odataOptions = ODataQueryParameterParser.Parse(odataParams);

        nativeOptions.Includes.Should().BeEquivalentTo(odataOptions.Includes);
    }

    // ========================
    // Helpers
    // ========================

    private static FilterGroup ParseDsl(string dsl)
    {
        var ast = DslAstParser.Parse(dsl);
        return DslFilterConverter.ToFilterGroup(ast);
    }

    private static void AssertEquivalentFilter(FilterGroup dsl, FilterGroup odata,
        string expectedField, string expectedOp, string expectedValue)
    {
        dsl.Filters.Should().HaveCount(1);
        odata.Filters.Should().HaveCount(1);

        dsl.Filters[0].Field.Should().Be(expectedField);
        odata.Filters[0].Field.Should().Be(expectedField);

        dsl.Filters[0].Operator.Should().Be(expectedOp);
        odata.Filters[0].Operator.Should().Be(expectedOp);

        dsl.Filters[0].Value.Should().Be(expectedValue);
        odata.Filters[0].Value.Should().Be(expectedValue);
    }
}
