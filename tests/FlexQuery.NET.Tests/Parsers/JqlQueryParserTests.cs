using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Parsers.Jql;

namespace FlexQuery.NET.Tests.Parsers;

public class JqlQueryParserTests
{
    private static FilterGroup JqlParse(string jql) =>
        new JqlQueryParser().Parse(jql);

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
}
