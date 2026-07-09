using FlexQuery.NET.Models;
using FlexQuery.NET.Models.Filters;
using FlexQuery.NET.Parsers.Jql;

namespace FlexQuery.NET.Tests.Parsers;

public class JqlQueryParserTests
{
    private static FilterGroup JqlParseFilter(string jql) =>
        new JqlQueryParser().Parse(jql);

    private static QueryOptions JqlParse(FlexQueryParameters parameters) =>
        new JqlQueryParser().Parse(parameters);

    private static QueryOptions JqlParse(string? filter = null, string? sort = null,
        string? groupBy = null, string? select = null, string? having = null,
        string? aggregates = null)
    {
        return JqlParse(new FlexQueryParameters
        {
            Filter = filter,
            Sort = sort,
            GroupBy = groupBy,
            Select = select,
            Having = having,
            Aggregates = aggregates
        });
    }

    [Fact]
    public void Jql_SimpleCondition_ParsedCorrectly()
    {
        var filter = JqlParseFilter("name = \"john\"");

        filter.Logic.Should().Be(LogicOperator.And);
        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("name");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Equal);
        filter.Filters[0].Value.Should().Be("john");
    }

    [Fact]
    public void Jql_AndCondition_ParsedCorrectly()
    {
        var filter = JqlParseFilter("name = \"john\" AND age >= 20");

        filter.Logic.Should().Be(LogicOperator.And);
        filter.Filters.Should().HaveCount(2);
        filter.Filters.Should().Contain(f => f.Field == "name" && f.Operator == FilterOperators.Equal && f.Value == "john");
        filter.Filters.Should().Contain(f => f.Field == "age" && f.Operator == FilterOperators.GreaterThanOrEq && f.Value == "20");
    }

    [Fact]
    public void Jql_OrCondition_ParsedCorrectly()
    {
        var filter = JqlParseFilter("name = \"john\" OR name = \"doe\"");

        filter.Logic.Should().Be(LogicOperator.Or);
        filter.Filters.Should().HaveCount(2);
        filter.Filters.Should().Contain(f => f.Field == "name" && f.Value == "john");
        filter.Filters.Should().Contain(f => f.Field == "name" && f.Value == "doe");
    }

    [Fact]
    public void Jql_NestedParentheses_ParsedCorrectly()
    {
        var filter = JqlParseFilter("(name = \"john\" OR name = \"doe\") AND age > 18");

        filter.Logic.Should().Be(LogicOperator.And);
        filter.Filters.Should().ContainSingle(f => f.Field == "age" && f.Operator == FilterOperators.GreaterThan && f.Value == "18");
        filter.Groups.Should().ContainSingle();
        filter.Groups[0].Logic.Should().Be(LogicOperator.Or);
        filter.Groups[0].Filters.Should().HaveCount(2);
    }

    [Fact]
    public void Jql_InOperator_ParsedCorrectly()
    {
        var filter = JqlParseFilter("status IN (\"active\",\"pending\")");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("status");
        filter.Filters[0].Operator.Should().Be(FilterOperators.In);
        filter.Filters[0].Value.Should().Be("active,pending");
    }

    [Fact]
    public void Jql_NestedPropertyPath_ParsedCorrectly()
    {
        var filter = JqlParseFilter("orders.customer.name CONTAINS \"john\"");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("orders.customer.name");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Contains);
        filter.Filters[0].Value.Should().Be("john");
    }

    [Fact]
    public void Jql_EmailAndNestedNumericCondition_ParsedCorrectly()
    {
        var filter = JqlParseFilter("email = \"ops@acmeretail.com\" AND orders.number = \"ORD-2026-0002\" AND orders.items.quantity > 2");

        filter.Logic.Should().Be(LogicOperator.And);
        filter.Filters.Should().HaveCount(3);
        filter.Filters.Should().Contain(f => f.Field == "email" && f.Operator == FilterOperators.Equal && f.Value == "ops@acmeretail.com");
        filter.Filters.Should().Contain(f => f.Field == "orders.number" && f.Operator == FilterOperators.Equal && f.Value == "ORD-2026-0002");
        filter.Filters.Should().Contain(f => f.Field == "orders.items.quantity" && f.Operator == FilterOperators.GreaterThan && f.Value == "2");
    }

    [Fact]
    public void Jql_BetweenOperator_ParsedCorrectly()
    {
        var filter = JqlParseFilter("age BETWEEN 18 AND 60");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("age");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Between);
        filter.Filters[0].Value.Should().Be("18,60");
    }

    [Fact]
    public void Jql_IsNullOperator_ParsedCorrectly()
    {
        var filter = JqlParseFilter("deletedAt IS NULL");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("deletedAt");
        filter.Filters[0].Operator.Should().Be(FilterOperators.IsNull);
        filter.Filters[0].Value.Should().BeNull();
    }

    [Fact]
    public void Jql_IsNotNullOperator_ParsedCorrectly()
    {
        var filter = JqlParseFilter("deletedAt IS NOT NULL");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("deletedAt");
        filter.Filters[0].Operator.Should().Be(FilterOperators.IsNotNull);
        filter.Filters[0].Value.Should().BeNull();
    }

    [Fact]
    public void Jql_LikeOperator_ParsedCorrectly()
    {
        var filter = JqlParseFilter("name LIKE \"%john%\"");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("name");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Like);
        filter.Filters[0].Value.Should().Be("%john%");
    }

    [Fact]
    public void Jql_AnyOperator_ParsedCorrectly()
    {
        var filter = JqlParseFilter("orders ANY total > 1000");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("orders");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Any);
        filter.Filters[0].Value.Should().Be("total:gt:1000");
    }

    [Fact]
    public void Jql_AllOperator_ParsedCorrectly()
    {
        var filter = JqlParseFilter("orders ALL status = \"completed\"");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("orders");
        filter.Filters[0].Operator.Should().Be(FilterOperators.All);
        filter.Filters[0].Value.Should().Be("status:eq:completed");
    }

    [Fact]
    public void Jql_CountOperator_ParsedCorrectly()
    {
        var filter = JqlParseFilter("orders COUNT > 5");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("orders");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Count);
        filter.Filters[0].Value.Should().Be("gt:5");
    }

    [Fact]
    public void Jql_ScopedAny_DotSyntax_ParsedToScopedFilter()
    {
        var filter = JqlParseFilter("orders.any(status = \"Cancelled\")");

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
        var filter = JqlParseFilter("orders.all(status = \"Active\")");

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
        var filter = JqlParseFilter("orders[status = \"Cancelled\"]");

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
        var filter = JqlParseFilter("orders.any(status = \"Cancelled\" AND total > 500)");

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
        var filter = JqlParseFilter("orders.any(status = \"Cancelled\" OR status = \"Refunded\")");

        filter.Filters.Should().ContainSingle();
        var cond = filter.Filters[0];
        cond.ScopedFilter.Should().NotBeNull();
        cond.ScopedFilter!.Logic.Should().Be(LogicOperator.Or);
        cond.ScopedFilter.Filters.Should().HaveCount(2);
    }

    [Fact]
    public void Jql_NestedScopedCollections_ParsedRecursively()
    {
        var filter = JqlParseFilter("orders.any(status = \"Cancelled\" AND orderItems.any(id = 101))");

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
        var filter = JqlParseFilter("name = \"Alice\" AND orders.any(total > 1000)");

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
        var filter = JqlParseFilter("orders.any(status = \"Cancelled\" AND total > 500)");

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
        var filter = JqlParseFilter("orders[status = \"Cancelled\"]");

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
        var filter = JqlParseFilter("orders.any(status = \"Cancelled\" AND orderItems.any(id = 101))");

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
        var filter = JqlParseFilter("orders.customer.name CONTAINS \"john\"");

        filter.Filters.Should().ContainSingle();
        filter.Filters[0].Field.Should().Be("orders.customer.name");
        filter.Filters[0].Operator.Should().Be(FilterOperators.Contains);
        filter.Filters[0].Value.Should().Be("john");
        filter.Filters[0].ScopedFilter.Should().BeNull();
    }

    // ─── Sort Parser Tests ────────────────────────────────────────────

    [Fact]
    public void JqlSort_SingleAsc_ParsedCorrectly()
    {
        var result = JqlParse(sort: "Name ASC");

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("Name");
        result.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void JqlSort_SingleDesc_ParsedCorrectly()
    {
        var result = JqlParse(sort: "CreatedDate DESC");

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("CreatedDate");
        result.Sort[0].Descending.Should().BeTrue();
    }

    [Fact]
    public void JqlSort_MultipleFields_ParsedCorrectly()
    {
        var result = JqlParse(sort: "Customer.Name DESC, CreatedDate ASC");

        result.Sort.Should().HaveCount(2);
        result.Sort[0].Field.Should().Be("Customer.Name");
        result.Sort[0].Descending.Should().BeTrue();
        result.Sort[1].Field.Should().Be("CreatedDate");
        result.Sort[1].Descending.Should().BeFalse();
    }

    [Fact]
    public void JqlSort_DefaultAsc_WhenNoDirectionSpecified()
    {
        var result = JqlParse(sort: "Name");

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("Name");
        result.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void JqlSort_NestedPropertyPath_ParsedCorrectly()
    {
        var result = JqlParse(sort: "Orders.Customer.Name ASC");

        result.Sort.Should().ContainSingle();
        result.Sort[0].Field.Should().Be("Orders.Customer.Name");
        result.Sort[0].Descending.Should().BeFalse();
    }

    [Fact]
    public void JqlSort_CaseInsensitiveDirection_ParsedCorrectly()
    {
        var result = JqlParse(sort: "Name desc, Age ASC");

        result.Sort[0].Descending.Should().BeTrue();
        result.Sort[1].Descending.Should().BeFalse();
    }

    [Fact]
    public void JqlSort_EmptyString_ReturnsEmptyList()
    {
        var result = JqlParse(sort: "");

        result.Sort.Should().BeEmpty();
    }

    [Fact]
    public void JqlSort_NullString_ReturnsEmptyList()
    {
        var result = JqlParse();

        result.Sort.Should().BeEmpty();
    }

    // ─── Aggregate Parser Tests ───────────────────────────────────────

    [Fact]
    public void JqlAggregate_SumWithAlias_ParsedCorrectly()
    {
        var result = JqlParse(aggregates: "SUM(Amount) AS TotalSales");

        result.Aggregates.Should().ContainSingle();
        result.Aggregates[0].Function.Should().Be("sum");
        result.Aggregates[0].Field.Should().Be("Amount");
        result.Aggregates[0].Alias.Should().Be("TotalSales");
    }

    [Fact]
    public void JqlAggregate_CountStar_ParsedCorrectly()
    {
        var result = JqlParse(aggregates: "COUNT(*)");

        result.Aggregates.Should().ContainSingle();
        result.Aggregates[0].Function.Should().Be("count");
        result.Aggregates[0].Field.Should().BeNull();
        result.Aggregates[0].Alias.Should().Be("Count");
    }

    [Fact]
    public void JqlAggregate_MultipleAggregates_ParsedCorrectly()
    {
        var result = JqlParse(aggregates: "SUM(Amount), AVG(Price), COUNT(*) AS Orders");

        result.Aggregates.Should().HaveCount(3);
        result.Aggregates[0].Function.Should().Be("sum");
        result.Aggregates[0].Field.Should().Be("Amount");
        result.Aggregates[1].Function.Should().Be("avg");
        result.Aggregates[1].Field.Should().Be("Price");
        result.Aggregates[2].Function.Should().Be("count");
        result.Aggregates[2].Field.Should().BeNull();
        result.Aggregates[2].Alias.Should().Be("Orders");
    }

    [Fact]
    public void JqlAggregate_NoAlias_GeneratesDefaultAlias()
    {
        var result = JqlParse(aggregates: "AVG(Price)");

        result.Aggregates.Should().ContainSingle();
        result.Aggregates[0].Function.Should().Be("avg");
        result.Aggregates[0].Field.Should().Be("Price");
        result.Aggregates[0].Alias.Should().Be("PriceAvg");
    }

    [Fact]
    public void JqlAggregate_NestedField_GeneratesCorrectAlias()
    {
        var result = JqlParse(aggregates: "SUM(Orders.Total)");

        result.Aggregates.Should().ContainSingle();
        result.Aggregates[0].Function.Should().Be("sum");
        result.Aggregates[0].Field.Should().Be("Orders.Total");
        result.Aggregates[0].Alias.Should().Be("OrdersTotalSum");
    }

    [Fact]
    public void JqlAggregate_CaseInsensitiveFunctions_ParsedCorrectly()
    {
        var result = JqlParse(aggregates: "sum(Amount), COUNT(*), Avg(Price)");

        result.Aggregates.Should().HaveCount(3);
        result.Aggregates[0].Function.Should().Be("sum");
        result.Aggregates[1].Function.Should().Be("count");
        result.Aggregates[2].Function.Should().Be("avg");
    }

    [Fact]
    public void JqlAggregate_MinMax_ParsedCorrectly()
    {
        var result = JqlParse(aggregates: "MIN(Date), MAX(Date)");

        result.Aggregates.Should().HaveCount(2);
        result.Aggregates[0].Function.Should().Be("min");
        result.Aggregates[0].Alias.Should().Be("DateMin");
        result.Aggregates[1].Function.Should().Be("max");
        result.Aggregates[1].Alias.Should().Be("DateMax");
    }

    // ─── GroupBy Parser Tests ─────────────────────────────────────────

    [Fact]
    public void JqlGroupBy_SingleField_ParsedCorrectly()
    {
        var result = JqlParse(groupBy: "Department");

        result.GroupBy.Should().ContainSingle();
        result.GroupBy[0].Should().Be("Department");
    }

    [Fact]
    public void JqlGroupBy_MultipleFields_ParsedCorrectly()
    {
        var result = JqlParse(groupBy: "Department,Category");

        result.GroupBy.Should().HaveCount(2);
        result.GroupBy.Should().ContainInOrder("Department", "Category");
    }

    [Fact]
    public void JqlGroupBy_NestedProperty_ParsedCorrectly()
    {
        var result = JqlParse(groupBy: "Customer.Region");

        result.GroupBy.Should().ContainSingle();
        result.GroupBy[0].Should().Be("Customer.Region");
    }

    // ─── Having Parser Tests ──────────────────────────────────────────

    [Fact]
    public void JqlHaving_CountGreaterThan_ParsedCorrectly()
    {
        var result = JqlParse(having: "COUNT(*) > 5");

        result.Having.Should().NotBeNull();
        result.Having!.Function.Should().Be("count");
        result.Having.Field.Should().BeNull();
        result.Having.Operator.Should().Be("gt");
        result.Having.Value.Should().Be("5");
    }

    [Fact]
    public void JqlHaving_SumGreaterThanValue_ParsedCorrectly()
    {
        var result = JqlParse(having: "SUM(Amount) > 1000");

        result.Having.Should().NotBeNull();
        result.Having!.Function.Should().Be("sum");
        result.Having.Field.Should().Be("Amount");
        result.Having.Operator.Should().Be("gt");
        result.Having.Value.Should().Be("1000");
    }

    [Fact]
    public void JqlHaving_AllOperators_ParsedCorrectly()
    {
        AssertHaving("COUNT(*) > 5", "gt", "5");
        AssertHaving("SUM(Amount) >= 1000", "gte", "1000");
        AssertHaving("AVG(Price) < 50", "lt", "50");
        AssertHaving("SUM(Amount) <= 100", "lte", "100");
        AssertHaving("COUNT(*) = 0", "eq", "0");
        AssertHaving("COUNT(*) != 0", "neq", "0");
    }

    private void AssertHaving(string havingExpr, string expectedOp, string expectedValue)
    {
        var result = JqlParse(having: havingExpr);
        result.Having.Should().NotBeNull();
        result.Having!.Operator.Should().Be(expectedOp);
        result.Having.Value.Should().Be(expectedValue);
    }

    [Fact]
    public void JqlHaving_FieldSpecific_ParsedCorrectly()
    {
        var result = JqlParse(having: "SUM(Orders.Total) > 500");

        result.Having.Should().NotBeNull();
        result.Having!.Function.Should().Be("sum");
        result.Having.Field.Should().Be("Orders.Total");
        result.Having.Operator.Should().Be("gt");
        result.Having.Value.Should().Be("500");
    }

    [Fact]
    public void JqlHaving_EmptyString_ReturnsNull()
    {
        var result = JqlParse(having: "");

        result.Having.Should().BeNull();
    }

    // ─── Integration Tests ────────────────────────────────────────────

    [Fact]
    public void JqlIntegration_AllParameters_ParsedCorrectly()
    {
        var result = JqlParse(
            filter: "Status = 'Shipped' AND Amount > 100",
            sort: "Customer.Name ASC, CreatedDate DESC",
            groupBy: "Customer.Region",
            aggregates: "SUM(Amount) AS TotalSales, COUNT(*) AS Orders, AVG(Amount)",
            having: "SUM(Amount) > 1000"
        );

        // Filter
        result.Filter.Should().NotBeNull();
        result.Filter!.Filters.Should().HaveCount(2);

        // Sort
        result.Sort.Should().HaveCount(2);
        result.Sort[0].Field.Should().Be("Customer.Name");
        result.Sort[0].Descending.Should().BeFalse();
        result.Sort[1].Field.Should().Be("CreatedDate");
        result.Sort[1].Descending.Should().BeTrue();

        // GroupBy
        result.GroupBy.Should().ContainSingle();
        result.GroupBy[0].Should().Be("Customer.Region");

        // Aggregates
        result.Aggregates.Should().HaveCount(3);
        result.Aggregates[0].Function.Should().Be("sum");
        result.Aggregates[0].Alias.Should().Be("TotalSales");
        result.Aggregates[1].Function.Should().Be("count");
        result.Aggregates[1].Alias.Should().Be("Orders");
        result.Aggregates[2].Function.Should().Be("avg");
        result.Aggregates[2].Alias.Should().Be("AmountAvg");

        // Having
        result.Having.Should().NotBeNull();
        result.Having!.Function.Should().Be("sum");
        result.Having.Operator.Should().Be("gt");
        result.Having.Value.Should().Be("1000");
    }
}
