using FlexQuery.NET.Constants;
using FlexQuery.NET.Parsers.MiniOData;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using FlexQuery.NET.Parsers.Dsl;
using FluentAssertions;

namespace FlexQuery.NET.Tests.MiniOData;

public class ODataDslEquivalenceTests
{
    [Fact]
    public void Equality_NativeDsl_And_OData_ProduceEquivalentAst()
    {
        var dslFilter = ParseDsl("name:eq:john");

        var odataFilter = ODataFilterParser.Parse("name eq 'john'");

        AssertEquivalentFilter(dslFilter, odataFilter, "name", FilterOperators.Equal, "john");
    }

    [Fact]
    public void Contains_NativeDsl_And_OData_ProduceEquivalentAst()
    {
        var dslFilter = ParseDsl("name:contains:john");
        var odataFilter = ODataFilterParser.Parse("contains(name,'john')");

        AssertEquivalentFilter(dslFilter, odataFilter, "name", FilterOperators.Contains, "john");
    }

    [Fact]
    public void StartsWith_NativeDsl_And_OData_ProduceEquivalentAst()
    {
        var dslFilter = ParseDsl("name:startswith:jo");
        var odataFilter = ODataFilterParser.Parse("startswith(name,'jo')");

        AssertEquivalentFilter(dslFilter, odataFilter, "name", FilterOperators.StartsWith, "jo");
    }

    [Fact]
    public void EndsWith_NativeDsl_And_OData_ProduceEquivalentAst()
    {
        var dslFilter = ParseDsl("email:endswith:.com");
        var odataFilter = ODataFilterParser.Parse("endswith(email,'.com')");

        AssertEquivalentFilter(dslFilter, odataFilter, "email", FilterOperators.EndsWith, ".com");
    }

    [Fact]
    public void GreaterThan_NativeDsl_And_OData_ProduceEquivalentAst()
    {
        var dslFilter = ParseDsl("age:gt:18");
        var odataFilter = ODataFilterParser.Parse("age gt 18");

        AssertEquivalentFilter(dslFilter, odataFilter, "age", FilterOperators.GreaterThan, "18");
    }

    [Fact]
    public void CompoundAnd_NativeDsl_And_OData_ProduceEquivalentAst()
    {
        var dslFilter = ParseDsl("age:gt:18&status:eq:active");
        var odataFilter = ODataFilterParser.Parse("age gt 18 and status eq 'active'");

        dslFilter.Logic.Should().Be(LogicOperator.And);
        odataFilter.Logic.Should().Be(LogicOperator.And);

        dslFilter.Filters.Should().HaveCount(2);
        odataFilter.Filters.Should().HaveCount(2);

        dslFilter.Filters[0].Field.Should().Be(odataFilter.Filters[0].Field);
        dslFilter.Filters[1].Field.Should().Be(odataFilter.Filters[1].Field);

        dslFilter.Filters[0].Operator.Should().Be(odataFilter.Filters[0].Operator);
        dslFilter.Filters[1].Operator.Should().Be(odataFilter.Filters[1].Operator);
    }

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

    [Fact]
    public void NullCheck_NativeDsl_And_OData_ProduceEquivalentAst()
    {
        var dslFilter = ParseDsl("deletedAt:isnull");
        var odataFilter = ODataFilterParser.Parse("deletedAt eq null");

        dslFilter.Filters[0].Field.Should().Be(odataFilter.Filters[0].Field);
        dslFilter.Filters[0].Operator.Should().Be(FilterOperators.IsNull);
        odataFilter.Filters[0].Operator.Should().Be(FilterOperators.IsNull);
    }

    [Fact]
    public void AnyRelationship_NativeDsl_And_OData_ProduceEquivalentAst()
    {
        var dslFilter = ParseDsl("orders.any(status:eq:Cancelled)");
        var odataFilter = ODataFilterParser.Parse("orders/any(o: o/status eq 'Cancelled')");

        dslFilter.Filters[0].Field.Should().Be("orders");
        odataFilter.Filters[0].Field.Should().Be("orders");

        dslFilter.Filters[0].Operator.Should().Be("any");
        odataFilter.Filters[0].Operator.Should().Be("any");

        dslFilter.Filters[0].ScopedFilter.Should().NotBeNull();
        odataFilter.Filters[0].ScopedFilter.Should().NotBeNull();

        dslFilter.Filters[0].ScopedFilter!.Filters[0].Field.Should().Be("status");
        odataFilter.Filters[0].ScopedFilter!.Filters[0].Field.Should().Be("status");

        dslFilter.Filters[0].ScopedFilter!.Filters[0].Value.Should().Be("Cancelled");
        odataFilter.Filters[0].ScopedFilter!.Filters[0].Value.Should().Be("Cancelled");
    }

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

    [Fact]
    public void OrderBy_NativeDsl_And_OData_ProduceEquivalentSortNodes()
    {
        var nativeParams = new FlexQueryParameters { Sort = "createdAt:desc" };
        var nativeOptions = QueryOptionsParser.Parse(nativeParams);

        var odataRequest = new MiniODataRequest { OrderBy = "createdAt desc" };
        var odataOptions = ODataQueryParameterParser.Parse(odataRequest);

        nativeOptions.Sort.Should().HaveCount(1);
        odataOptions.Sort.Should().HaveCount(1);

        nativeOptions.Sort[0].Field.Should().Be(odataOptions.Sort[0].Field);
        nativeOptions.Sort[0].Descending.Should().Be(odataOptions.Sort[0].Descending);
    }

    [Fact]
    public void Select_NativeDsl_And_OData_ProduceEquivalentProjection()
    {
        var nativeParams = new FlexQueryParameters { Select = "id,name,email" };
        var nativeOptions = QueryOptionsParser.Parse(nativeParams);

        var odataRequest = new MiniODataRequest { Select = "id,name,email" };
        var odataOptions = ODataQueryParameterParser.Parse(odataRequest);

        nativeOptions.Select.Should().BeEquivalentTo(odataOptions.Select);
    }

    [Fact]
    public void Pagination_NativeDsl_And_OData_ProduceEquivalentPaging()
    {
        var nativeParams = new FlexQueryParameters { Page = 3, PageSize = 10 };
        var nativeOptions = QueryOptionsParser.Parse(nativeParams);

        var odataRequest = new MiniODataRequest { Top = 10, Skip = 20 };
        var odataOptions = ODataQueryParameterParser.Parse(odataRequest);

        nativeOptions.Paging.PageSize.Should().Be(odataOptions.Paging.PageSize);
        nativeOptions.Paging.Page.Should().Be(odataOptions.Paging.Page);
    }

    [Fact]
    public void Include_NativeDsl_And_OData_ProduceEquivalentIncludes()
    {
        var nativeParams = new FlexQueryParameters { Include = "orders,profile" };
        var nativeOptions = QueryOptionsParser.Parse(nativeParams);

        var odataRequest = new MiniODataRequest { Expand = "orders,profile" };
        var odataOptions = ODataQueryParameterParser.Parse(odataRequest);

        nativeOptions.Includes.Should().BeEquivalentTo(odataOptions.Includes);
    }

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
